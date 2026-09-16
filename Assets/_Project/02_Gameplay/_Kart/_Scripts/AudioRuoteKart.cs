using UnityEngine;
using ArcadeKart.Core;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Suono delle ruote del kart in loop mentre si muove.
    /// Due set di parametri fusi in continuo con il peso fase del KartController:
    /// CAMMINATA (boost rilasciato) = pitch e volume ridotti;
    /// CORSA (boost mouse sx tenuto) = pitch e volume pieni.
    /// A kart fermo il suono sfuma e va in pausa.
    /// In volo o sulle pareti-rampa (lancio skate) il suono si spegne; al
    /// ritorno a terra un one-shot di atterraggio suona mentre le ruote
    /// ripartono immediatamente (sorgenti separate, nessuna attesa).
    /// Un urto forte contro un muro/parete (OnImpattoMuro del controller)
    /// riproduce un one-shot di urto a volume proporzionale alla forza.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioRuoteKart : MonoBehaviour
    {
        [Header("Riferimenti")]
        [Tooltip("KartController da cui leggere velocita' e fase (camminata/corsa); se vuoto viene cercato su questo GameObject e nei padri.")]
        [SerializeField] private KartController kart;

        [Tooltip("AudioSource del suono ruote; se vuoto usa quello su questo GameObject (aggiunto in automatico se manca).")]
        [SerializeField] private AudioSource sorgenteAudio;

        [Tooltip("Clip del suono ruote; se vuota lascia quella gia' assegnata all'AudioSource.")]
        [SerializeField] private AudioClip suonoRuote;

        [Header("Camminata (boost rilasciato)")]
        [Tooltip("Volume del suono ruote in fase camminata.")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeCamminata = 0.45f;

        [Tooltip("Pitch in fase camminata: sotto 1 il suono viene riprodotto piu' lentamente (ruote piu' lente).")]
        [SerializeField] private float pitchCamminata = 0.85f;

        [Header("Corsa (boost mouse sx tenuto)")]
        [Tooltip("Volume del suono ruote in fase corsa.")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeCorsa = 1f;

        [Tooltip("Pitch in fase corsa: 1 = velocita' di riproduzione normale.")]
        [SerializeField] private float pitchCorsa = 1f;

        [Header("Transizioni")]
        [Tooltip("Velocita' planare minima del kart per tenere il suono attivo; sotto questa soglia sfuma e va in pausa.")]
        [SerializeField] private float sogliaMovimento = 0.3f;

        [Tooltip("Velocita' di smorzamento del blend volume/pitch e del fade in pausa. Piu' alto = transizioni piu' rapide.")]
        [SerializeField] private float velocitaTransizione = 8f;

        [Header("Modulazione velocita'")]
        [Tooltip("Se attivo, pitch e volume si modulano anche sul rapporto tra velocita' reale del kart e soffitto corrente: reagiscono alle decelerazioni (es. inversioni a 180 gradi) e alle ripartenze, oltre alla fase camminata/corsa.")]
        [SerializeField] private bool modulaConVelocita = true;

        [Tooltip("Fattore pitch a velocita' quasi nulla rispetto al soffitto: 0.6 = a kart quasi fermo il suono gira al 60% della velocita' di fase corrente.")]
        [Range(0f, 1f)]
        [SerializeField] private float pitchAVelocitaZero = 0.6f;

        [Tooltip("Fattore volume a velocita' quasi nulla rispetto al soffitto.")]
        [Range(0f, 1f)]
        [SerializeField] private float volumeAVelocitaZero = 0.5f;

        [Header("Atterraggio")]
        [Tooltip("Suono one-shot riprodotto al ritorno a terra dopo un volo o una parete-rampa (fdf03fcd.s133).")]
        [SerializeField] private AudioClip suonoAtterraggio;

        [Tooltip("AudioSource per il suono di atterraggio; se vuoto viene creato in automatico su questo GameObject.")]
        [SerializeField] private AudioSource sorgenteAtterraggio;

        [Tooltip("Volume del suono di atterraggio: sopra 1 = leggermente piu' forte del suono ruote.")]
        [Range(0f, 2f)]
        [SerializeField] private float volumeAtterraggio = 1.15f;

        [Tooltip("Pitch del suono di atterraggio (1 = velocita' di riproduzione normale).")]
        [SerializeField] private float pitchAtterraggio = 1f;

        [Tooltip("Tempo minimo di volo (sec) per riprodurre l'atterraggio: filtra i micro-stacchi di terreno e lo spawn (0 = sempre).")]
        [SerializeField] private float tempoVoloMinimo = 0.1f;

        [Header("Urto col muro")]
        [Tooltip("Suono one-shot riprodotto quando il kart sbatte forte contro un muro/parete NON di tipo ground (fdf03fcd.s134; le pareti-rampa skate su layer ground non lo triggerano). L'evento arriva da KartController (OnImpattoMuro), gia' filtrato dal suo impactThreshold.")]
        [SerializeField] private AudioClip suonoUrto;

        [Tooltip("AudioSource per il suono di urto; se vuoto viene creato in automatico su questo GameObject.")]
        [SerializeField] private AudioSource sorgenteUrto;

        [Tooltip("Soglia aggiuntiva di forza (m/s) oltre quella del controller (impactThreshold): sotto, il suono non parte. 0 = usa solo la soglia del controller.")]
        [SerializeField] private float sogliaUrtoAggiuntiva = 0f;

        [Tooltip("Volume dell'urto appena sopra soglia.")]
        [Range(0f, 2f)]
        [SerializeField] private float volumeUrtoMinimo = 0.6f;

        [Tooltip("Volume dell'urto alla forza di riferimento (forzaVolumeMassimo): oltre, resta al massimo.")]
        [Range(0f, 2f)]
        [SerializeField] private float volumeUrtoMassimo = 1.2f;

        [Tooltip("Forza (m/s) a cui il volume urto raggiunge volumeUrtoMassimo.")]
        [SerializeField] private float forzaVolumeMassimo = 10f;

        [Tooltip("Pitch del suono di urto (1 = velocita' di riproduzione normale).")]
        [SerializeField] private float pitchUrto = 1f;

        [Tooltip("Tempo minimo (sec) fra due suoni di urto: evita ri-trigger sfregando lungo una curva del muro.")]
        [SerializeField] private float cooldownUrto = 0.25f;

        // Volume corrente smorzato: governa il fade a zero quando il kart e'
        // fermo (niente tagli netti = niente click in coda al suono).
        private float volumeAttuale;

        // Stato atterraggio: tempo cumulato senza terra e flag del frame
        // precedente, per il rilevamento della transizione volo/parete -> terra.
        private float tempoInAria;
        private bool eraATerra = true;

        // Timestamp dell'ultimo suono di urto, per il cooldown anti ri-trigger.
        private float ultimoTempoUrto = -999f;

        private void Awake()
        {
            if (kart == null)
                kart = GetComponentInParent<KartController>();

            if (sorgenteAudio == null)
                sorgenteAudio = GetComponent<AudioSource>();

            if (sorgenteAudio == null)
                sorgenteAudio = gameObject.AddComponent<AudioSource>();

            if (suonoRuote != null)
                sorgenteAudio.clip = suonoRuote;

            sorgenteAudio.loop = true;
            sorgenteAudio.playOnAwake = false;

            volumeAttuale = 0f;
            sorgenteAudio.volume = 0f;

            // Sorgente dedicata all'atterraggio (one-shot separato dal loop:
            // le due riproduzioni non si escludono a vicenda).
            if (sorgenteAtterraggio == null)
                sorgenteAtterraggio = gameObject.AddComponent<AudioSource>();

            if (suonoAtterraggio != null)
                sorgenteAtterraggio.clip = suonoAtterraggio;

            sorgenteAtterraggio.loop = false;
            sorgenteAtterraggio.playOnAwake = false;
            // Stessa cura spaziale delle ruote: fonte 3D agganciata al kart.
            sorgenteAtterraggio.spatialBlend = 1f;
            sorgenteAtterraggio.dopplerLevel = 0f;

            // Sorgente dedicata all'urto col muro (one-shot separato).
            if (sorgenteUrto == null)
                sorgenteUrto = gameObject.AddComponent<AudioSource>();

            if (suonoUrto != null)
                sorgenteUrto.clip = suonoUrto;

            sorgenteUrto.loop = false;
            sorgenteUrto.playOnAwake = false;
            sorgenteUrto.spatialBlend = 1f;
            sorgenteUrto.dopplerLevel = 0f;

            eraATerra = true;
            tempoInAria = 0f;
        }

        private void OnEnable()
        {
            if (kart != null)
                kart.OnImpattoMuro.AddListener(GestisciImpattoMuro);
        }

        private void OnDisable()
        {
            if (kart != null)
                kart.OnImpattoMuro.RemoveListener(GestisciImpattoMuro);
        }

        // Da KartController.OnImpattoMuro: forza (m/s) dell'urto contro una
        // superficie verticale. Volume proporzionale alla forza, con cooldown
        // anti ri-trigger (sfregamenti lungo curve del muro).
        private void GestisciImpattoMuro(float forza)
        {
            if (sorgenteUrto == null || suonoUrto == null)
                return;
            if (forza < sogliaUrtoAggiuntiva)
                return;
            if (Time.time - ultimoTempoUrto < cooldownUrto)
                return;

            ultimoTempoUrto = Time.time;
            float rapporto = Mathf.Clamp01(
                (forza - sogliaUrtoAggiuntiva)
                / Mathf.Max(forzaVolumeMassimo - sogliaUrtoAggiuntiva, 0.001f));
            sorgenteUrto.pitch = pitchUrto;
            sorgenteUrto.volume = Mathf.Lerp(volumeUrtoMinimo, volumeUrtoMassimo, rapporto);
            sorgenteUrto.PlayOneShot(suonoUrto);
        }

        private void Update()
        {
            if (kart == null || sorgenteAudio == null || sorgenteAudio.clip == null)
                return;

            // Peso fase (0 = camminata, 1 = corsa) gia' smorzato dal
            // controller: e' lo stesso con cui si fondono grip e sterzo,
            // cosi' l'audio segue la stessa fase percepita dal resto del kart.
            float pesoCorsa = kart.PesoCorsaFase;

            float volumeTarget = Mathf.Lerp(volumeCamminata, volumeCorsa, pesoCorsa);
            float pitchTarget = Mathf.Lerp(pitchCamminata, pitchCorsa, pesoCorsa);

            float velocitaAssoluta = Mathf.Abs(kart.CurrentSpeed);

            // Modulazione sulla velocita' REALE: il rapporto con il soffitto
            // corrente cala quando il kart decelera (es. inversioni a 180
            // gradi) e risale quando riprende velocita', cosi' il suono segue
            // il ritmo del kart invece di restare bloccato sui valori di fase.
            if (modulaConVelocita)
            {
                float rapporto = Mathf.Clamp01(
                    velocitaAssoluta
                    / Mathf.Max(kart.SoffittoVelocitaAttuale, 0.01f));
                pitchTarget *= Mathf.Lerp(pitchAVelocitaZero, 1f, rapporto);
                volumeTarget *= Mathf.Lerp(volumeAVelocitaZero, 1f, rapporto);
            }

            // A terra "camminabile": esclude il volo E le pareti-rampa. Nel
            // lancio skate IsGrounded resta vero (lo SphereCast becca la
            // rampa sotto la parete), quindi serve anche LancioSkateAttivo.
            bool kartATerra = kart.IsGrounded && !kart.LancioSkateAttivo;

            // Transizione volo/parete -> terra: one-shot di atterraggio. Le
            // ruote ripartono IMMEDIATAMENTE: l'one-shot suona sopra il loop
            // che rientra col fade esistente, su sorgente separata.
            if (kartATerra)
            {
                if (!eraATerra && tempoInAria >= tempoVoloMinimo
                    && sorgenteAtterraggio != null && suonoAtterraggio != null)
                {
                    sorgenteAtterraggio.pitch = pitchAtterraggio;
                    sorgenteAtterraggio.volume = volumeAtterraggio;
                    sorgenteAtterraggio.PlayOneShot(suonoAtterraggio);
                }
                tempoInAria = 0f;
            }
            else
            {
                tempoInAria += Time.deltaTime;
            }
            eraATerra = kartATerra;

            bool inMovimento = velocitaAssoluta >= sogliaMovimento;
            bool suonoRuoteAttivo = kartATerra && inMovimento;
            if (!suonoRuoteAttivo)
                volumeTarget = 0f;

            // Smorzamento esponenziale (stesso schema del soffitto nel controller).
            float passo = 1f - Mathf.Exp(-velocitaTransizione * Time.deltaTime);
            volumeAttuale = Mathf.Lerp(volumeAttuale, volumeTarget, passo);
            sorgenteAudio.pitch = Mathf.Lerp(sorgenteAudio.pitch, pitchTarget, passo);
            sorgenteAudio.volume = volumeAttuale;

            if (suonoRuoteAttivo)
            {
                if (!sorgenteAudio.isPlaying)
                    sorgenteAudio.Play();
            }
            else if (sorgenteAudio.isPlaying && volumeAttuale < 0.001f)
            {
                sorgenteAudio.Pause();
            }
        }
    }
}
