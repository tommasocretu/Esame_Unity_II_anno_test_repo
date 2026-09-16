using UnityEngine;
using ArcadeKart.Core;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Anima gli arti del personaggio dentro il kart.
    /// Ogni arto e' una linea (LineRenderer) a 3 punti:
    /// partenza (spalla/anca) -> punto centrale (gomito/ginocchio, calcolato via script) -> arrivo (mano/piede).
    /// Le gambe simulano una camminata o una corsa: il piede percorre un arco (scivola di lato
    /// e si alza nella meta' "in aria" del ciclo) e il ginocchio lo segue.
    /// La cadenza NON dipende dalla velocita' del kart ma dalla fase: camminata in guida normale,
    /// corsa mentre il boost (mouse sinistro) e' tenuto. A kart fermo le gambe restano ferme.
    /// Il verso del passo segue la direzione di movimento del kart vista da camera (niente moonwalk).
    /// Con [ExecuteAlways] in editor le linee restano disegnate sulla posa di riposo e si aggiornano
    /// spostando le ancore; l'animazione vera e propria gira solo in Play.
    /// </summary>
    [DefaultExecutionOrder(200)] // dopo KartController e SpriteBillboard: le ancore sono gia' aggiornate nel frame
    [ExecuteAlways] // gira anche in edit mode: le linee si ridisegnano mentre si muovono gli slider
    public class ArtiPersonaggio : MonoBehaviour
    {
        [System.Serializable]
        public class Arto
        {
            [Tooltip("Ancora di partenza (spalla o anca), di solito figlia di Base2D per seguire lo sprite.")]
            public Transform partenza;

            [Tooltip("Ancora di arrivo (mano o piede), di solito figlia del kart per restare fissa sul volante/pedali.")]
            public Transform arrivo;

            [Tooltip("LineRenderer dell'arto; se vuoto viene creato automaticamente come figlio.")]
            public LineRenderer linea;

            [Tooltip("Curvatura del punto centrale (gomito/ginocchio), in metri: positiva piega da un lato, negativa dall'altro.")]
            public float curvatura = 0.05f;
        }

        [System.Serializable]
        public class Gamba : Arto
        {
            [Tooltip("Sfasamento della pedalata (0-1 giri): 0.5 rende le gambe in controfase.")]
            [Range(0f, 1f)] public float sfasamento = 0f;
        }

        [Header("Riferimenti")]
        [Tooltip("KartController da cui leggere la velocita'; se vuoto viene cercato nei padri.")]
        [SerializeField] private KartController kart;

        [Header("Aspetto linee")]
        [Tooltip("Materiale nero delle linee (LineaNera.mat).")]
        [SerializeField] private Material materialeLinea;
        [Tooltip("Spessore delle linee delle braccia.")]
        [SerializeField] private float spessoreBraccia = 0.02f;
        [Tooltip("Spessore delle linee delle gambe.")]
        [SerializeField] private float spessoreGambe = 0.022f;
        [Tooltip("Ordine di sorting: con 10 le linee disegnano sopra lo sprite del personaggio (che e' a 0).")]
        [SerializeField] private int ordineSorting = 10;

        [Header("Braccia")]
        [SerializeField] private Arto braccioSinistro;
        [SerializeField] private Arto braccioDestro;

        [Header("Gambe")]
        [SerializeField] private Gamba gambaSinistra;
        [SerializeField] private Gamba gambaDestra;

        [Header("Camminata e corsa")]
        [Tooltip("Cicli al secondo nella fase camminata (kart in movimento, boost spento). Valore fisso, non dipende dalla velocita'.")]
        [SerializeField] private float frequenzaCamminata = 2f;
        [Tooltip("Cicli al secondo nella fase corsa (boost mouse sinistro tenuto). Valore fisso.")]
        [SerializeField] private float frequenzaCorsa = 5f;
        [Tooltip("Moltiplicatore delle ampiezze di passo e ginocchio nella fase corsa (l'alzo del piede ha il suo campo dedicato ampiezzaAlzoCorsa).")]
        [SerializeField] private float scalaAmpiezzeCorsa = 1.4f;
        [Tooltip("Velocita' minima del kart (m/s) per avere le gambe in movimento; sotto soglia restano ferme sulle ancore.")]
        [SerializeField] private float sogliaMovimento = 0.3f;

        [Header("Ampiezze (camminata)")]
        [Tooltip("Escursione laterale del piede durante il passo, in metri.")]
        [SerializeField] private float ampiezzaPasso = 0.03f;
        [Tooltip("Quanto si alza il piede nella fase in aria del passo, in metri (fase camminata).")]
        [SerializeField] private float ampiezzaAlzo = 0.035f;
        [Tooltip("Quanto si alza il piede nella fase corsa, in metri. Viene fuso dolcemente con ampiezzaAlzo tra le due fasi.")]
        [SerializeField] private float ampiezzaAlzoCorsa = 0.3f;
        [Tooltip("Escursione massima del ginocchio durante il passo, in metri.")]
        [SerializeField] private float ampiezzaMassima = 0.045f;

        [Header("Direzione passo")]
        [Tooltip("Velocita' minima del kart (m/s) per invertire il verso del passo; sotto soglia resta l'ultimo verso (anti-jitter).")]
        [SerializeField] private float sogliaDirezione = 0.5f;

        // Fase corrente della pedalata, espressa in giri (0-1).
        private float fase;

        // Rigidbody del kart: serve la velocita' mondo per capire verso dove il personaggio "cammina" sullo schermo.
        private Rigidbody rbKart;

        // Verso corrente dello scorrimento del passo: +1 o -1 secondo la direzione di movimento vista da camera.
        private float versoPasso = 1f;

        // Peso animato (0-1) della fase corsa: fonde cadenza e ampiezze tra camminata e corsa senza scatti.
        private float pesoCorsa;

        // Peso animato (0-1) del movimento: porta ampiezze e fase a zero quando il kart e' fermo.
        private float pesoMovimento;

        // Fase corrente del passo (0-1), pubblica per chi vuole sincronizzarsi con la camminata/corsa (es. BobBusto).
        public float FasePasso => fase;

        // Intensita' animata del movimento (0 a kart fermo, 1 a gambe in movimento): stessa rampa dolce delle gambe.
        public float IntensitaMovimento => pesoMovimento;

        private void Awake()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();
            if (kart != null) rbKart = kart.GetComponent<Rigidbody>();

            Prepara(braccioSinistro, spessoreBraccia, "Linea_BraccioSX");
            Prepara(braccioDestro, spessoreBraccia, "Linea_BraccioDX");
            Prepara(gambaSinistra, spessoreGambe, "Linea_GambaSX");
            Prepara(gambaDestra, spessoreGambe, "Linea_GambaDX");
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            bool inEditor = !Application.isPlaying;

            // Fase camminata/corsa: dipende dal boost (mouse sx tenuto), NON dalla velocita' del kart.
            // In edit mode il kart e' fermo e senza input: le linee restano sulla posa di riposo.
            bool inCorsa = kart != null && kart.IsBoosting;
            // Peso animato della fase: cadenza e ampiezze fondono dolcemente tra camminata e corsa.
            pesoCorsa = Mathf.MoveTowards(pesoCorsa, inCorsa ? 1f : 0f, 5f * dt);
            float frequenzaCorrente = Mathf.Lerp(frequenzaCamminata, frequenzaCorsa, pesoCorsa);
            float scalaAmpiezze = Mathf.Lerp(1f, scalaAmpiezzeCorsa, pesoCorsa);
            // L'alzo del piede ha valori espliciti per fase: si fonde con lo stesso peso della corsa.
            float alzoCorrente = Mathf.Lerp(ampiezzaAlzo, ampiezzaAlzoCorsa, pesoCorsa);

            // Le gambe si muovono solo se il kart avanza davvero: sotto soglia restano ferme sulle ancore.
            bool inMovimento = kart != null && Mathf.Abs(kart.CurrentSpeed) > sogliaMovimento;
            // Peso animato del movimento: entrando/uscendo dal fermo ampiezze e fase passano per zero.
            pesoMovimento = Mathf.MoveTowards(pesoMovimento, inMovimento ? 1f : 0f, 8f * dt);

            // La fase avanza solo a gambe in movimento.
            fase = Mathf.Repeat(fase + frequenzaCorrente * pesoMovimento * dt, 1f);

            // Verso della camera: in Play la game camera, in editor la Scene view (anteprima fedele all'angolo in uso).
            Camera cam = Camera.main;
#if UNITY_EDITOR
            if (inEditor && UnityEditor.SceneView.lastActiveSceneView != null && UnityEditor.SceneView.lastActiveSceneView.camera != null)
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            Vector3 versoCamera = cam != null
                ? (cam.transform.position - transform.position).normalized
                : Vector3.forward;

            // Base visibile a schermo, calcolata una volta per frame:
            // suSchermo = verticale proiettata sul piano perpendicolare alla camera,
            // lateraleSchermo = asse orizzontale a schermo, perpendicolare a vista e verticale
            // (con la camera dietro il kart punta verso sinistra dello schermo).
            Vector3 suSchermo = ProiettaSuSchermo(Vector3.up, versoCamera);
            suSchermo = suSchermo.sqrMagnitude > 0.001f
                ? suSchermo.normalized
                : ProiettaSuSchermo(Vector3.forward, versoCamera).normalized;
            Vector3 lateraleSchermo = Vector3.Cross(suSchermo, versoCamera).normalized;

            // Verso del passo: segue la direzione di movimento reale del kart proiettata sull'asse orizzontale dello schermo.
            if (rbKart != null)
            {
                Vector3 velocitaMondo = rbKart.linearVelocity;
                if (velocitaMondo.sqrMagnitude > sogliaDirezione * sogliaDirezione)
                {
                    // Il piede "in aria" (che definisce il verso della camminata) si muove verso -lateraleSchermo * verso:
                    // per camminare nella direzione di movimento il verso deve essere l'OPPOSTO del segno di
                    // Dot(velocita', lateraleSchermo), perche' lateraleSchermo punta a sinistra dello schermo.
                    float versoObiettivo = Vector3.Dot(velocitaMondo, lateraleSchermo) >= 0f ? -1f : 1f;
                    // Virata dolce: passando per 0 i piedi rientrano al centro invece di specchiarsi di scatto.
                    versoPasso = Mathf.MoveTowards(versoPasso, versoObiettivo, 6f * dt);
                }
            }

            // Aspetto linee riapplicato ogni frame: spessore, materiale e sorting restano modificabili live dall'inspector.
            ApplicaAspetto(braccioSinistro, spessoreBraccia);
            ApplicaAspetto(braccioDestro, spessoreBraccia);
            ApplicaAspetto(gambaSinistra, spessoreGambe);
            ApplicaAspetto(gambaDestra, spessoreGambe);

            DisegnaBraccio(braccioSinistro, versoCamera);
            DisegnaBraccio(braccioDestro, versoCamera);
            float sfasamentoSX = gambaSinistra != null ? gambaSinistra.sfasamento : 0f;
            float sfasamentoDX = gambaDestra != null ? gambaDestra.sfasamento : 0.5f;
            DisegnaGamba(gambaSinistra, versoCamera, suSchermo, lateraleSchermo, scalaAmpiezze * pesoMovimento, alzoCorrente * pesoMovimento, fase + sfasamentoSX);
            DisegnaGamba(gambaDestra, versoCamera, suSchermo, lateraleSchermo, scalaAmpiezze * pesoMovimento, alzoCorrente * pesoMovimento, fase + sfasamentoDX);
        }

        /// <summary>Braccio: spalla -> gomito (punto medio piegato) -> mano.</summary>
        private void DisegnaBraccio(Arto arto, Vector3 versoCamera)
        {
            if (!Valido(arto)) return;

            Vector3 partenza = arto.partenza.position;
            Vector3 arrivo = arto.arrivo.position;
            Vector3 suSchermo = DirezioneVisibile(arrivo - partenza, versoCamera);
            Vector3 gomito = Vector3.Lerp(partenza, arrivo, 0.5f) + suSchermo * arto.curvatura;

            arto.linea.SetPosition(0, partenza);
            arto.linea.SetPosition(1, gomito);
            arto.linea.SetPosition(2, arrivo);
        }

        /// <summary>
        /// Gamba: anca -> ginocchio -> piede. Il piede percorre un arco a mezz'aria:
        /// scivola di lato mentre e' sollevato (meta' "in aria" del ciclo) e torna appoggiato nell'altra meta'.
        /// Lo scorrimento laterale segue versoPasso, cioe' la direzione di movimento del kart vista da camera.
        /// scalaAmpiezze fonde passo e ginocchio tra le fasi, alzoPiede e' l'alzo del piede fuso camminata/corsa
        /// (entrambi valgono 0 a kart fermo). Il ginocchio segue il piede animato e si alza in sincrono con lui.
        /// </summary>
        private void DisegnaGamba(Gamba gamba, Vector3 versoCamera, Vector3 suSchermo, Vector3 lateraleSchermo, float scalaAmpiezze, float alzoPiede, float faseGamba)
        {
            if (!Valido(gamba)) return;

            Vector3 anca = gamba.partenza.position;
            Vector3 piedeBase = gamba.arrivo.position;

            // Arco del passo: lo scorrimento laterale e' continuo, l'alzo esiste solo nella meta' in aria (arco semicircolare).
            float angolo = faseGamba * Mathf.PI * 2f;
            Vector3 piedeAnimato = piedeBase
                + lateraleSchermo * (Mathf.Cos(angolo) * ampiezzaPasso * scalaAmpiezze * versoPasso)
                + suSchermo * (Mathf.Max(0f, Mathf.Sin(angolo)) * alzoPiede);

            // Il ginocchio e' il punto medio tra anca e piede animato, con alzo sincronizzato al piede.
            Vector3 suGinocchio = DirezioneVisibile(piedeAnimato - anca, versoCamera);
            float alzoGinocchio = Mathf.Max(0f, Mathf.Sin(angolo)) * ampiezzaMassima * scalaAmpiezze;
            Vector3 ginocchio = Vector3.Lerp(anca, piedeAnimato, 0.5f)
                + suGinocchio * (gamba.curvatura + alzoGinocchio);

            gamba.linea.SetPosition(0, anca);
            gamba.linea.SetPosition(1, ginocchio);
            gamba.linea.SetPosition(2, piedeAnimato);
        }

        /// <summary>Proietta un vettore sul piano perpendicolare alla direzione di vista.</summary>
        private static Vector3 ProiettaSuSchermo(Vector3 vettore, Vector3 versoCamera)
        {
            return vettore - versoCamera * Vector3.Dot(vettore, versoCamera);
        }

        /// <summary>
        /// Direzione perpendicolare all'arto ma visibile a schermo:
        /// la proietta sul piano perpendicolare alla camera, cosi' la piega non "svanisce" guardando l'arto di fronte.
        /// </summary>
        private static Vector3 DirezioneVisibile(Vector3 direzioneArto, Vector3 versoCamera)
        {
            Vector3 dir = direzioneArto.sqrMagnitude > 0.000001f ? direzioneArto.normalized : Vector3.forward;

            // Parte dal "su" del mondo proiettato sul piano dello schermo.
            Vector3 suSchermo = Vector3.up - versoCamera * Vector3.Dot(Vector3.up, versoCamera);
            Vector3 risultato = suSchermo - dir * Vector3.Dot(suSchermo, dir);

            // Caso limite: arto quasi parallelo alla verticale, si piega di lato rispetto alla camera.
            if (risultato.sqrMagnitude < 0.0001f) risultato = Vector3.Cross(versoCamera, dir);

            return risultato.sqrMagnitude > 0.0001f ? risultato.normalized : Vector3.up;
        }

        /// <summary>L'arto e' utilizzabile solo se ha ancora, arrivo e linea assegnati.</summary>
        private static bool Valido(Arto arto)
        {
            return arto != null && arto.partenza != null && arto.arrivo != null && arto.linea != null;
        }

        /// <summary>
        /// Crea (o riusa) il LineRenderer di un arto e ne fissa le impostazioni strutturali.
        /// Nome deterministico: se esiste gia' un figlio con lo stesso nome viene riusato.
        /// </summary>
        private void Prepara(Arto arto, float spessore, string nomeLinea)
        {
            if (arto == null) return;

            // Riusa la linea gia' assegnata, altrimenti cerca il figlio con quel nome, altrimenti lo crea.
            if (arto.linea == null)
            {
                Transform trovato = transform.Find(nomeLinea);
                arto.linea = trovato != null ? trovato.GetComponent<LineRenderer>() : null;
            }
            if (arto.linea == null)
            {
                var go = new GameObject(nomeLinea);
                go.transform.SetParent(transform, false);
                arto.linea = go.AddComponent<LineRenderer>();
            }

            var linea = arto.linea;
            linea.positionCount = 3;
            linea.useWorldSpace = true;
            linea.alignment = LineAlignment.View; // la linea si volge sempre verso la camera
            linea.numCapVertices = 2; // punte arrotondate
            linea.numCornerVertices = 0;

            ApplicaAspetto(arto, spessore);

            // Aggiorna subito i punti cosi' la linea e' visibile anche fuori dal Play Mode.
            if (arto.partenza != null && arto.arrivo != null)
            {
                linea.SetPosition(0, arto.partenza.position);
                linea.SetPosition(1, arto.partenza.position);
                linea.SetPosition(2, arto.arrivo.position);
            }
        }

        /// <summary>
        /// Impostazioni grafiche delle linee, riapplicate a ogni LateUpdate:
        /// cosi' spessore, materiale e ordine di sorting sono modificabili in diretta dall'inspector.
        /// </summary>
        private void ApplicaAspetto(Arto arto, float spessore)
        {
            if (!Valido(arto)) return;

            var linea = arto.linea;
            linea.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            linea.widthMultiplier = spessore;
            linea.startColor = Color.black;
            linea.endColor = Color.black;
            if (materialeLinea != null) linea.sharedMaterial = materialeLinea;
            linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linea.receiveShadows = false;
            linea.sortingOrder = ordineSorting;
        }

#if UNITY_EDITOR
        [ContextMenu("Rigenera linee in editor")]
        private void RigeneraLineeInEditor()
        {
            if (kart == null) kart = GetComponentInParent<KartController>();
            Prepara(braccioSinistro, spessoreBraccia, "Linea_BraccioSX");
            Prepara(braccioDestro, spessoreBraccia, "Linea_BraccioDX");
            Prepara(gambaSinistra, spessoreGambe, "Linea_GambaSX");
            Prepara(gambaDestra, spessoreGambe, "Linea_GambaDX");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
