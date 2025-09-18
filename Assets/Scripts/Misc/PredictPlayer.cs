using UnityEngine;

/// <summary>
/// Tracks the player's position and velocity using a simplified Kalman filter.
/// Provides smoothed predictions of where the player will be in the future.
/// Attach this to an empty GameObject and assign the Player transform.
/// </summary>
public class PredictPlayer : MonoBehaviour
{
    [Header("Player Reference")]
    public Transform player;

    [Header("Kalman Settings")]
    [Range(0f, 1f)] public float processNoise = 0.05f;     // Trust in movement model
    [Range(0f, 1f)] public float measurementNoise = 0.1f;  // Trust in observed player pos
    public float predictionTime = 1.0f;                    // Seconds ahead to predict

    // Kalman state: position + velocity
    private Vector3 estimatedPos;
    private Vector3 estimatedVel;
    private Vector3 lastPlayerPos;

    // Per-axis uncertainty (diagonal covariance)
    private Vector3 covariance;

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("PlayerPredictor: No player assigned!");
            enabled = false;
            return;
        }

        estimatedPos = player.position;
        estimatedVel = Vector3.zero;
        lastPlayerPos = player.position;
        covariance = Vector3.one * 0.1f; // small initial uncertainty
    }

    void Update()
    {
        Vector3 measuredPos = player.position;
        float dt = Time.deltaTime;

        // --- Prediction Step ---
        Vector3 predictedPos = estimatedPos + estimatedVel * dt;
        covariance += Vector3.one * processNoise;

        // --- Measurement Update ---
        Vector3 innovation = measuredPos - predictedPos;
        Vector3 gain = new Vector3(
            covariance.x / (covariance.x + measurementNoise),
            covariance.y / (covariance.y + measurementNoise),
            covariance.z / (covariance.z + measurementNoise)
        );

        estimatedPos = predictedPos + Vector3.Scale(gain, innovation);

        // Smoothed velocity
        float velSmoothing = 0.5f;
        Vector3 measuredVel = (measuredPos - lastPlayerPos) / dt;
        estimatedVel = Vector3.Lerp(estimatedVel, measuredVel, 1 - velSmoothing);

        covariance = Vector3.Scale(Vector3.one - gain, covariance);

        lastPlayerPos = measuredPos;
    }


    /// <summary>
    /// Returns predicted player position X seconds in the future.
    /// </summary>
    public Vector3 GetPredictedPosition(float lookahead = -1f)
    {
        if (lookahead <= 0f)
            lookahead = predictionTime;

        Vector3 predicted = estimatedPos + estimatedVel * lookahead;

        // Clamp to full map bounds
        predicted.x = Mathf.Clamp(predicted.x, -600f, 600f);
        predicted.z = Mathf.Clamp(predicted.z, -600f, 600f);

        return predicted;
    }



    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            // Draw estimated player position (green)
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(estimatedPos, 20f);

            // Draw predicted lookahead position (red)
            Vector3 futurePos = GetPredictedPosition();
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(futurePos, 20f);
        }
    }
}
