using UnityEngine;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Fa saltellare su e giu' l'oggetto a cui e' attaccato (su Base2D: il busto del personaggio),
    /// in sincrono con il ciclo dei passi di ArtiPersonaggio: un rimbalzo per ogni alzata del piede.
    /// Oscilla il proprio localPosition.y attorno alla posa originale, senza toccare la rotazione
    /// (nessun conflitto con SpriteBillboard). Le sue variabili sono separate da quelle degli arti.
    /// In editor resta statico: anima solo in Play.
    /// </summary>
    public class BobBusto : MonoBehaviour
    {
        [Header("Riferimenti")]
        [Tooltip("ArtiPersonaggio da cui leggere la fase del passo e l'intensita' del movimento; va collegato a mano (non e' un antenato di questo oggetto).")]
        [SerializeField] private ArtiPersonaggio arti;

        [Header("Bob del busto")]
        [Tooltip("Quanto il busto si solleva a ogni passo, in metri. Negativo = il busto si abbassa invece che salire.")]
        [SerializeField] private float ampiezzaBob = 0.03f;
        [Tooltip("Sposta il punto di riposo su/giu' in metri, se serve allineare lo sprite.")]
        [SerializeField] private float offsetRiposo = 0f;

        // Posa originale dell'oggetto: il bob oscilla attorno a questa.
        private Vector3 posaRiposo;
        private bool posaSalvata;

        private void Awake()
        {
            SalvaPosa();
            if (arti == null) arti = GetComponentInParent<ArtiPersonaggio>();
        }

        private void LateUpdate()
        {
            if (arti == null) return;
            SalvaPosa();

            // Intensita' del movimento: 0 a kart fermo (busto in posa di riposo), 1 a gambe in movimento.
            // Uso la stessa rampa dolce delle gambe, cosi' busto e passi partono/fermano insieme.
            float intensita = arti.IntensitaMovimento;

            // Un rimbalzo per ogni alzata del piede: |sin| della fase del passo ha un picco
            // sia sul sollevamento del piede sinistro sia su quello destro.
            float faseRimbalzo = Mathf.Abs(Mathf.Sin(arti.FasePasso * Mathf.PI * 2f));
            float scostamento = faseRimbalzo * ampiezzaBob * intensita;

            Vector3 posizione = posaRiposo;
            posizione.y += offsetRiposo + scostamento;
            transform.localPosition = posizione;
        }

        // Memorizza la posa di riposo una sola volta: il bob oscilla sempre attorno a questa.
        private void SalvaPosa()
        {
            if (posaSalvata) return;
            posaRiposo = transform.localPosition;
            posaSalvata = true;
        }
    }
}
