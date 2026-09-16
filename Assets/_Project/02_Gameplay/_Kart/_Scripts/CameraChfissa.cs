using UnityEngine;

/// <summary>
/// Fa ruotare lo Sprite Renderer in modo che guardi sempre verso la camera principale.
/// Ideale per sprite 2D in scene 3D (effetto billboard).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteBillboard : MonoBehaviour
{
    [Tooltip("Se true, usa la Camera.main; se false, assegna manualmente la camera")]
    public bool useMainCamera = true;

    [Tooltip("Camera di riferimento (usata se useMainCamera = false)")]
    public Camera targetCamera;

    [Tooltip("Se true, allinea solo l'asse Y (rotazione orizzontale), mantenendo l'up del mondo")]
    public bool lockYAxisOnly = false;

    private Transform _cameraTransform;

    void Start()
    {
        if (useMainCamera)
        {
            _cameraTransform = Camera.main?.transform;
            if (_cameraTransform == null)
                Debug.LogWarning("[SpriteBillboard] Camera.main non trovata!");
        }
        else
        {
            if (targetCamera == null)
                Debug.LogWarning("[SpriteBillboard] Nessuna camera assegnata in targetCamera!");
            else
                _cameraTransform = targetCamera.transform;
        }
    }

    void LateUpdate()
    {
        if (_cameraTransform == null) return;

        if (lockYAxisOnly)
        {
            // Guarda la camera mantenendo l'asse Y fisso (utile per sprite verticali)
            Vector3 directionToCamera = _cameraTransform.position - transform.position;
            directionToCamera.y = 0f; // ignora differenza verticale
            if (directionToCamera.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
                transform.rotation = targetRotation;
            }
        }
        else
        {
            // Guarda esattamente la camera (billboard completo)
            transform.LookAt(_cameraTransform.position);
        }
    }
}
