using System.Collections;
using UnityEngine;

namespace ArcadeKart.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour
    {
        [Header("Pickup")]
        [SerializeField, Tooltip("Tipo logico dell'oggetto raccolto. Es: coin, gem, key.")]
        private string visualType = "coin";

        [SerializeField, Tooltip("Tag richiesto per poter raccogliere l'oggetto.")]
        private string requiredTag = "Player";

        [Header("Fly To Stack")]
        [SerializeField, Tooltip("Durata del volo verso lo stack root.")]
        private float flyDuration = 0.35f;

        [SerializeField, Tooltip("Altezza massima della parabola.")]
        private float arcHeight = 1.25f;

        [SerializeField, Tooltip("Rotazione visiva durante il volo.")]
        private Vector3 spinDegreesPerSecond = new Vector3(0f, 360f, 0f);

        [SerializeField, Tooltip("Se true, durante il volo l'oggetto si riduce leggermente.")]
        private bool shrinkOnFly = true;

        [SerializeField, Tooltip("Scala finale relativa durante il volo.")]
        private float endScaleMultiplier = 0.75f;

        [Header("Suono")]
        [SerializeField, Tooltip("Suono riprodotto al momento della raccolta (al tocco).")]
        private AudioClip suonoRaccolta;

        [SerializeField, Tooltip("Volume del suono di raccolta.")]
        private float volumeRaccolta = 1f;

        private bool collected;
        private Collider cachedCollider;
        private Rigidbody cachedRb;

        // Stato iniziale salvato per il respawn: il pickup viene disattivato
        // anziche' distrutto alla raccolta, e ripristinato quando la radice
        // del livello (genitore) viene riattivata. Salviamo lo stato locale
        // in Awake cosi' OnDisable puo' rimettere l'oggetto esattamente come
        // era al primo avvio, pronto per essere raccolto di nuovo.
        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;
        private Vector3 initialLocalScale;
        private bool initialColliderEnabled;
        private bool initialRbIsKinematic;
        private bool initialRbDetectCollisions;
        private bool hasInitialRbState;

        private void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            cachedRb = GetComponent<Rigidbody>();

            if (cachedCollider != null && !cachedCollider.isTrigger)
                cachedCollider.isTrigger = true;

            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;
            initialLocalScale = transform.localScale;

            if (cachedCollider != null)
                initialColliderEnabled = cachedCollider.enabled;

            if (cachedRb != null)
            {
                initialRbIsKinematic = cachedRb.isKinematic;
                initialRbDetectCollisions = cachedRb.detectCollisions;
                hasInitialRbState = true;
            }
        }

        // Chiamato sia quando la raccolta finisce (SetActive(false) finale
        // della coroutine) sia quando il livello genitore viene disattivato
        // (TornaAlMenu, eventualmente a meta' di un volo in corso). In ogni
        // caso riportiamo il pickup allo stato iniziale: cosi' alla
        // riattivazione della radice del livello e' di nuovo al suo posto,
        // con collider attivo e non collected, raccoglibile da zero.
        private void OnDisable()
        {
            StopAllCoroutines();

            collected = false;

            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;
            transform.localScale = initialLocalScale;

            if (cachedCollider != null)
                cachedCollider.enabled = initialColliderEnabled;

            if (cachedRb != null && hasInitialRbState)
            {
                cachedRb.linearVelocity = Vector3.zero;
                cachedRb.angularVelocity = Vector3.zero;
                cachedRb.isKinematic = initialRbIsKinematic;
                cachedRb.detectCollisions = initialRbDetectCollisions;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected)
                return;

            if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
                return;

            KartCollectedStack stack = other.GetComponentInParent<KartCollectedStack>();
            if (stack == null)
            {
                Debug.LogWarning("[Pickup] Il player non ha KartCollectedStack.", other);
                return;
            }

            if (stack.StackRoot == null)
            {
                Debug.LogWarning("[Pickup] Il kart non ha StackRoot assegnato.", stack);
                return;
            }

            StartCoroutine(FlyToStackRoutine(stack));
        }

        private IEnumerator FlyToStackRoutine(KartCollectedStack stack)
        {
            collected = true;

            // Suono di raccolta al tocco: PlayClipAtPoint crea un AudioSource
            // temporaneo indipendente dal pickup, cosi' il suono non viene
            // troncato quando l'oggetto si disattiva a fine volo (SetActive(false)).
            if (suonoRaccolta != null)
                AudioSource.PlayClipAtPoint(suonoRaccolta, transform.position, volumeRaccolta);

            if (cachedCollider != null)
                cachedCollider.enabled = false;

            if (cachedRb != null)
            {
                cachedRb.linearVelocity = Vector3.zero;
                cachedRb.angularVelocity = Vector3.zero;
                cachedRb.isKinematic = true;
                cachedRb.detectCollisions = false;
            }

            Transform target = stack.StackRoot;
            Transform tr = transform;

            Vector3 startPos = tr.position;
            Quaternion startRot = tr.rotation;
            Vector3 startScale = tr.localScale;

            float t = 0f;
            float duration = Mathf.Max(0.01f, flyDuration);

            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float eased = Mathf.Clamp01(t);

                Vector3 endPos = target.position;
                Vector3 pos = Vector3.Lerp(startPos, endPos, eased);

                float arc = 4f * arcHeight * eased * (1f - eased);
                pos.y += arc;

                tr.position = pos;
                tr.rotation = startRot * Quaternion.Euler(spinDegreesPerSecond * (duration * eased));

                if (shrinkOnFly)
                {
                    float scaleMul = Mathf.Lerp(1f, endScaleMultiplier, eased);
                    tr.localScale = startScale * scaleMul;
                }

                yield return null;
            }

            stack.AddCollectedItem(visualType);

            // Disattiviamo invece di distruggere: quando la radice del livello
            // verra' riattivata (CaricaLivello dopo un TornaAlMenu), OnDisable
            // avra' gia' ripristinato lo stato iniziale e il pickup sara' di
            // nuovo raccoglibile. SetActive(false) chiama OnDisable che fa il
            // reset, quindi qui non serve ripetere la logica di ripristino.
            gameObject.SetActive(false);
        }
    }
}
