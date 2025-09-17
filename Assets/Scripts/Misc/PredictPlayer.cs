using System.Collections;
using UnityEngine;

public class PredictPlayer : MonoBehaviour
{
    [Header("References")]
    public Transform player;        // object to track
    public Transform indicator;     // visual indicator where tank should camp/aim
    public Transform tank;          // the tank that will move to intercept (for intercept math)

    [Header("Prediction")]
    public float predictHorizon = 1.2f;   // tuned for tank combat
    public float modelMemory = 0.15f;     // matches ~0.15s accel smoothing
    public float softening = 0.01f;       // stable weighting


    [Header("Tank")]
    public float tankSpeed = 6f;     // units/sec (movement speed of the tank)

    // internal
    Vector3 prevPos;
    Vector3 prevVel;
    Vector3 velocity;
    Vector3 accel;
    float prevTime;

    private void Start()
    {
        if (player != null)
            prevPos = player.position;
        prevTime = Time.time;
    }

    private void LateUpdate()
    {
        UpdateKinematics();
        Vector3 blendedPrediction = MixedModelPredict(predictHorizon);
        // Compute reachable intercept point given tank speed
        Vector3 intercept = ComputeInterceptPoint(tank != null ? tank.position : transform.position, blendedPrediction, predictHorizon, tankSpeed);
        indicator.position = intercept;
    }

    void UpdateKinematics()
    {
        float now = Time.time;
        float dt = now - prevTime;
        if (dt <= Mathf.Epsilon) return;

        Vector3 pos = player.position;
        velocity = (pos - prevPos) / dt;
        accel = (velocity - prevVel) / Mathf.Max(dt, 1e-6f);

        prevPos = pos;
        prevVel = velocity;
        prevTime = now;
    }

    // Build three candidate predictions and weight them by recent fit
    Vector3 MixedModelPredict(float horizon)
    {
        // Candidate 1: Constant Velocity (CV)
        Vector3 cvPos = player.position + velocity * horizon;

        // Candidate 2: Constant Acceleration (CA)
        Vector3 caPos = player.position + velocity * horizon + 0.5f * accel * horizon * horizon;

        // Candidate 3: Constant Turn (CT) - approximate by rotating velocity by angular velocity about z
        // Estimate angular velocity around up axis from change in velocity direction
        Vector3 v = velocity;
        float vMag = v.magnitude;
        float angVel = 0f;
        if (vMag > 1e-4f && prevVel.magnitude > 1e-4f)
        {
            // signed angular velocity (2D)
            float angleNow = Mathf.Atan2(v.y, v.x);
            float anglePrev = Mathf.Atan2(prevVel.y, prevVel.x);
            float dAng = Mathf.DeltaAngle(anglePrev * Mathf.Rad2Deg, angleNow * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float dt = Mathf.Max(Time.time - prevTime + Time.deltaTime, 1e-6f); // conservative dt
            angVel = dAng / dt;
        }
        // rotate the velocity by angVel * horizon
        float theta = angVel * horizon;
        Vector3 rotatedVel = new Vector3(
            v.x * Mathf.Cos(theta) - v.y * Mathf.Sin(theta),
            v.x * Mathf.Sin(theta) + v.y * Mathf.Cos(theta),
            v.z
        );
        Vector3 ctPos = player.position + (rotatedVel) * horizon;

        // Evaluate model fit using how well each model would have predicted the last observed position:
        // Simple proxy: forward-propagate backwards by small dt and compare to previous pos (lower error => higher weight).
        // We'll use the "one-step" innovation: how well each model would predict current position from prev state.
        float dtCheck = Mathf.Min(modelMemory, Mathf.Max(Time.deltaTime, 0.02f));
        Vector3 predFromPrevCV = prevPos + prevVel * dtCheck;
        Vector3 predFromPrevCA = prevPos + prevVel * dtCheck + 0.5f * accel * dtCheck * dtCheck;
        // For CT we rotate prevVel by small angle
        float smallTheta = angVel * dtCheck;
        Vector3 rotPrevVel = new Vector3(
            prevVel.x * Mathf.Cos(smallTheta) - prevVel.y * Mathf.Sin(smallTheta),
            prevVel.x * Mathf.Sin(smallTheta) + prevVel.y * Mathf.Cos(smallTheta),
            prevVel.z
        );
        Vector3 predFromPrevCT = prevPos + rotPrevVel * dtCheck;

        float errCV = (player.position - predFromPrevCV).sqrMagnitude;
        float errCA = (player.position - predFromPrevCA).sqrMagnitude;
        float errCT = (player.position - predFromPrevCT).sqrMagnitude;

        // Convert errors to weights (lower error -> higher weight). Use softmax-style.
        float iCV = 1f / (errCV + softening);
        float iCA = 1f / (errCA + softening);
        float iCT = 1f / (errCT + softening);

        float sum = iCV + iCA + iCT;
        Vector3 blended = (cvPos * iCV + caPos * iCA + ctPos * iCT) / sum;
        return blended;
    }

    // Solve intercept point: find tau >= 0 such that |(p + v*tau) - shooterPos| = tankSpeed * tau
    Vector3 ComputeInterceptPoint(Vector3 shooterPos, Vector3 predictedPos, float predHorizon, float shooterSpeed)
    {
        // We'll use the blended predicted position as a target position at predHorizon.
        // But we should derive a linear target motion from current velocity for better intercept:
        Vector3 r0 = predictedPos - player.position; // displacement from now to predicted position
        // approximate target velocity toward predictedPos over predHorizon:
        Vector3 targetVel = predHorizon > 1e-6f ? r0 / predHorizon : Vector3.zero;

        // relative vector from shooter to target now:
        Vector3 r = player.position - shooterPos;

        // Solve quadratic: (v·v - s^2) t^2 + 2(r·v) t + r·r = 0
        float a = Vector3.Dot(targetVel, targetVel) - shooterSpeed * shooterSpeed;
        float b = 2f * Vector3.Dot(r, targetVel);
        float c = Vector3.Dot(r, r);

        float tau = -1f;

        if (Mathf.Abs(a) < 1e-6f)
        {
            // linear: a ~ 0 => b*t + c = 0
            if (Mathf.Abs(b) > 1e-6f)
            {
                float t = -c / b;
                if (t > 0f) tau = t;
            }
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc >= 0f)
            {
                float sqrtD = Mathf.Sqrt(disc);
                float t1 = (-b + sqrtD) / (2f * a);
                float t2 = (-b - sqrtD) / (2f * a);
                // pick smallest positive
                float tpos = Mathf.Infinity;
                if (t1 > 0f) tpos = Mathf.Min(tpos, t1);
                if (t2 > 0f) tpos = Mathf.Min(tpos, t2);
                if (tpos != Mathf.Infinity) tau = tpos;
            }
        }

        // Fallbacks: if no positive root, either target too fast or unreachable; use short lead or direct predictedPos
        if (tau < 0f)
        {
            // Try to move to predictedPos (clamped) or a short time lead
            return predictedPos;
        }

        // clamp tau so we don't pick an absurdly large intercept time; also we might want to favor nearer intercepts
        tau = Mathf.Min(tau, 5f); // cap 5 seconds ahead, tuneable

        Vector3 interceptPoint = player.position + targetVel * tau;
        return interceptPoint;
    }
}
