using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Mediapipe.Unity.PoseLandmarkSDK
{
    /// <summary>
    /// Burst-compiled jobs for parallel filtering of landmarks.
    /// Significantly reduces CPU overhead for 33+ landmarks.
    /// </summary>
    public static class FilteringJobs
    {
        [BurstCompile]
        public struct OneEuroFilterJob : IJobParallelFor
        {
            // Input
            [ReadOnly] public NativeArray<float3> RawPositions;
            [ReadOnly] public NativeArray<float> Timestamps;
            [ReadOnly] public float CurrentTime;
            
            // Parameters (Shared)
            [ReadOnly] public float MinCutoff;
            [ReadOnly] public float Beta;
            [ReadOnly] public float DCutoff;

            // State (Read/Write)
            public NativeArray<float3> FilteredPositions;
            public NativeArray<float3> LastRawPositions;
            // Internal filter states: [index*2] = x_state, [index*2+1] = dx_state for each dimension
            // Layout: x_val, dx_val, y_val, dy_val, z_val, dz_val
            [NativeDisableParallelForRestriction]
            public NativeArray<float> InternalState; 
            public NativeArray<bool> IsInitialized;

            public void Execute(int index)
            {
                float3 rawPos = RawPositions[index];
                
                if (!IsInitialized[index])
                {
                    FilteredPositions[index] = rawPos;
                    LastRawPositions[index] = rawPos;
                    IsInitialized[index] = true;
                    
                    // Reset internal state
                    for (int i = 0; i < 3; i++)
                    {
                        int baseIdx = index * 6 + i * 2;
                        InternalState[baseIdx] = rawPos[i]; // Value
                        InternalState[baseIdx + 1] = 0f;    // Derivative
                    }
                    return;
                }

                float dt = CurrentTime - Timestamps[index];
                if (dt <= 0f) dt = 1f / 60f;

                float3 filteredPos = new float3(0,0,0);

                // Process X, Y, Z
                for (int i = 0; i < 3; i++)
                {
                    float val = rawPos[i];
                    float lastVal = LastRawPositions[index][i];
                    
                    // Estimate velocity (dx)
                    float dx = (val - lastVal) / dt;
                    
                    // Filter flux
                    int dxStateIdx = index * 6 + i * 2 + 1;
                    float edx = LowPassFilter(dx, Alpha(dt, DCutoff), ref InternalState, dxStateIdx);

                    // Start cut-off
                    float cutoff = MinCutoff + Beta * math.abs(edx);
                    
                    // Filter signal
                    int xStateIdx = index * 6 + i * 2;
                    float filteredVal = LowPassFilter(val, Alpha(dt, cutoff), ref InternalState, xStateIdx);
                    
                    filteredPos[i] = filteredVal;
                }

                FilteredPositions[index] = filteredPos;
                LastRawPositions[index] = rawPos;
            }

            private float Alpha(float dt, float cutoff)
            {
                float tau = 1.0f / (2.0f * math.PI * cutoff);
                return 1.0f / (1.0f + tau / dt);
            }

            private float LowPassFilter(float val, float alpha, ref NativeArray<float> state, int idx)
            {
                float lastVal = state[idx];
                float newVal = alpha * val + (1.0f - alpha) * lastVal;
                state[idx] = newVal;
                return newVal;
            }
        }

        [BurstCompile]
        public struct KalmanFilterJob : IJobParallelFor
        {
            // Input
            [ReadOnly] public NativeArray<float3> Measurement;
            [ReadOnly] public float DeltaTime;
            
            // Parameters (Shared)
            [ReadOnly] public float ProcessNoise;
            [ReadOnly] public float MeasurementNoise;

            // State (Read/Write)
            // State: [x, y, z, vx, vy, vz, ax, ay, az] (9 floats per landmark)
            [NativeDisableParallelForRestriction]
            public NativeArray<float> State; 
            // P (Covariance): 9x9 simplified diagonal (9 floats per landmark)
            [NativeDisableParallelForRestriction]
            public NativeArray<float> Covariance; 
            public NativeArray<bool> IsInitialized;
            public NativeArray<float3> Result;

            public void Execute(int index)
            {
                int stateOffset = index * 9;
                int covOffset = index * 9; // Diagonal only
                float3 measures = Measurement[index];

                if (!IsInitialized[index])
                {
                    // Init State
                    State[stateOffset + 0] = measures.x;
                    State[stateOffset + 1] = measures.y;
                    State[stateOffset + 2] = measures.z;
                    // Velocities/Accels = 0
                    for(int i=3; i<9; i++) State[stateOffset + i] = 0;

                    // Init Covariance (High uncertainty)
                    for(int i=0; i<9; i++) Covariance[covOffset + i] = 1.0f;
                    
                    IsInitialized[index] = true;
                    Result[index] = measures;
                    return;
                }

                float dt = DeltaTime;
                
                // --- PREDICT ---
                // X = F*X
                // x = x + v*dt + 0.5*a*dt^2
                // v = v + a*dt
                for (int i = 0; i < 3; i++)
                {
                    float p = State[stateOffset + i];
                    float v = State[stateOffset + i + 3];
                    float a = State[stateOffset + i + 6];
                    
                    State[stateOffset + i] = p + v * dt + 0.5f * a * dt * dt;
                    State[stateOffset + i + 3] = v + a * dt;
                }

                // P = F*P*F' + Q
                // Simplified diagonal constant-accel model update
                for (int i = 0; i < 9; i++)
                {
                    Covariance[covOffset + i] += ProcessNoise * dt;
                }

                // --- UPDATE ---
                // K = P / (P + R) (Scalar approximation per dimension)
                // x = x + K * (z - x)
                // P = (1 - K) * P
                
                // Initialize finalPos explicitly to zero
                float3 finalPos = new float3(0,0,0);
                
                for (int i = 0; i < 3; i++)
                {
                    float p = Covariance[covOffset + i];
                    float k = p / (p + MeasurementNoise);
                    float residual = measures[i] - State[stateOffset + i];
                    
                    // Update state (Position only directly measured)
                    State[stateOffset + i] += k * residual;
                    
                    // Velocity update correlation (simplified)
                    State[stateOffset + i + 3] += k * residual * dt; 

                    // Update Covariance
                    Covariance[covOffset + i] = (1.0f - k) * p;
                    
                    // Update finalPos components
                    finalPos[i] = State[stateOffset + i];
                }
                
                Result[index] = finalPos;
            }
        }
    }
}
