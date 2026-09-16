using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeKart.Gameplay
{
    public class KartCollectedStack : MonoBehaviour
    {
        [Serializable]
        public class VisualEntry
        {
            [Tooltip("Tipo logico dell'oggetto. Es: coin, gem, key.")]
            public string visualType = "coin";

            [Tooltip("Prefab da mostrare nella torre.")]
            public GameObject visualPrefab;

            [Tooltip("Scala locale del prefab instanziato.")]
            public Vector3 localScale = Vector3.one;
        }

        [Header("References")]
        [SerializeField, Tooltip("Punto sotto cui creare la torre. Consigliato: un figlio di Grafica.")]
        private Transform stackRoot;

        [Header("Layout")]
        [SerializeField, Tooltip("Offset locale del primo elemento della torre.")]
        private Vector3 baseLocalOffset = new Vector3(0f, 0f, 0f);

        [SerializeField, Tooltip("Distanza verticale tra un elemento e il successivo.")]
        private float verticalSpacing = 0.35f;

        [SerializeField, Tooltip("Rotazione locale degli elementi impilati.")]
        private Vector3 localEuler = Vector3.zero;

        [Header("Limit")]
        [SerializeField, Tooltip("Numero massimo di oggetti visibili nella torre.")]
        private int maxItems = 5;

        [Header("Sicurezza Fisica")]
        [SerializeField, Tooltip("Layer su cui forzare i cloni della torre. Deve essere un layer inerte che NON collide col kart (Vehicle/ground) ne' coi suoi SphereCast/Raycast di ground check, cosi' una torre alta non puo' bloccare il kart in tunnel/passaggi stretti. Default: Oggeto (9).")]
        private int stackLayer = 9;

        [Header("Type Mapping")]
        [SerializeField, Tooltip("Associazione tipo -> prefab visivo.")]
        private List<VisualEntry> visuals = new List<VisualEntry>();

        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        // Stato di oscillazione (molla smorzata) per ogni elemento della torre,
        // tenuto allineato a spawnedItems: pitch = inclinazione avanti/indietro
        // (asse X locale della torre), roll = inclinazione laterale (asse Z).
        private struct ItemWobble
        {
            public float pitch;
            public float pitchVel;
            public float roll;
            public float rollVel;
        }

        private readonly List<ItemWobble> wobble = new List<ItemWobble>();

        [Header("Reattivita' Carrello")]
        [SerializeField, Tooltip("Rigidita' della molla: quanta forza richiede per inclinare la torre. Alto = torre rigida, basso = torre molle/flessibile.")]
        private float wobbleStiffness = 120f;

        [SerializeField, Tooltip("Smorzamento della molla. Piu' alto = l'oscillazione si esaurisce in fretta (niente rimbalzo); piu' basso = 'gelatina' con piu' rimbalzi. Per comportamento sotto-smorzato tenerlo sotto sqrt(4*stiffness).")]
        private float wobbleDamping = 12f;

        [SerializeField, Tooltip("Inclinazione massima in gradi per ogni elemento della torre.")]
        private float maxWobbleAngle = 35f;

        [SerializeField, Tooltip("Quanto il braccio della forza cresce con l'altezza. 0 = tutti gli elementi si inclinano uguali; >0 = gli elementi in cima oscillano di piu' (effetto carrello della spesa, torre alta scalpita in cima).")]
        private float leverPerIndex = 0.35f;

        [SerializeField, Tooltip("Moltiplicatore dell'accelerazione longitudinale sul pitch (avanti/indietro).")]
        private float longAccelToPitch = 1.6f;

        [SerializeField, Tooltip("Moltiplicatore dell'accelerazione laterale sul roll (sx/dx). Valore negativo inverte il verso di piegamento.")]
        private float latAccelToRoll = 1.1f;

        [SerializeField, Tooltip("Impulso angolare (deg/sec) applicato alla torre quando il kart prende un urto (KartController.OnImpact). Distribuito con lever arm, quindi la cima salta di piu'.")]
        private float impactImpulse = 140f;

        [SerializeField, Tooltip("Componente casuale di roll sull'urto (impulso laterale random), per dare varietà ai rimbalzi.")]
        private float impactLateralRandom = 80f;

        [SerializeField, Tooltip("Viene aggiunto alla velocita' angolare di yaw del kart per alimentare il roll in curva, anche senza grossi delta di velocita' laterale. Aiuta a sentire le sterzate.")]
        private float yawToRoll = 0.6f;

        [SerializeField, Tooltip("Smorzamento aggiuntivo quando la torre e' ferma, per evitare micro-jitter numerici.")]
        private float restDampingBoost = 1.5f;

        [Header("Velocita' Costante (vita in corsa)")]
        [SerializeField, Tooltip("Lean statico all'indietro proporzionale alla velocita' forward (resistenza aria fittizia). A 22 m/s con 0.25 produce ~5.5 gradi * lever di inclinazione in cima. 0 = disattivato. La pila reale su un carrello in corsa non e' perfettamente dritta, si sdraia un po' all'indietro.")]
        private float windToPitch = 0.25f;

        [SerializeField, Tooltip("Ampiezza in gradi della micro-vibrazione residua a velocita' (simula asperita' del terreno). Viene scalata da vibrationSpeedScale: al raqquarso ��n SPEED_SCALE arriva al 100% di questa ampiezza. 0 = niente vibrazione.")]
        private float vibrationAmplitude = 0.6f;

        [SerializeField, Tooltip("Frequenza (Hz) della micro-vibrazione. Default ~6 Hz, simile a un kart su asfalto. Too basso = ondeggiamento lento finto; too alto = jitter metallico.")]
        private float vibrationFrequency = 6f;

        [SerializeField, Tooltip("Velocita' (m/s) forward a cui la vibrazione raggiunge il 100% della ampiezza. Sotto questa soglia la vibrazione scala linearmente verso 0, per via del kart fermo che non scalpita.")]
        private float vibrationSpeedScale = 14f;

        [SerializeField, Tooltip("Numero di elementi dal basso della torre che restano rigidi (niente oscillazione gelatina). 0 = tutti oscillano; >0 = i primi N dal fondo sono fissi, utile perche' la base di una pila reale e' stabile mentre solo la cima scalpita.")]
        private int rigidBaseCount = 1;

        [Header("Smoothing (anti-scatti)")]
        [SerializeField, Tooltip("Costante di tempo (secondi) del low-pass sulle accelerazioni del kart prima di alimentare la molla. 0 = nessun filtro (molta reattivita', tipica per inversioni a U provooca scatti); >0 = accel input smorzata -> la molla parte in modo piu fluido. Default 0.08s.")]
        private float inputSmoothing = 0.08f;

        [SerializeField, Tooltip("Velocita' massima (gradi/sec) del visualizzato: la torre non puo' MAI inclinarsi piu velocemente di questo, qualunque cosa accada nella molla o nell'input. Uccide gli scatti visivi su sali/scendi/snap. 0 = disattivato (segue la molla grezza). Default 220 deg/sec.")]
        private float maxWobbleDisplaySpeed = 220f;

        [SerializeField, Tooltip("Soglia minima di magnitudo (m/s) dell'urto (KartController.OnImpact) per applicare l'impulso alla torre. Piu' alto = ignora i piccoli sobbalzi su rampe bumposa. Default 7.")]
        private float impactMinMagnitude = 7f;

        [SerializeField, Tooltip("Intervallo minimo (secondi) fra due impulsi d'urto consecutivi. Su una rampa bumposa con 5+ picchi/sec questo evita che la torre venga martellata ed impazzisca. Default 0.12s.")]
        private float impactCooldown = 0.12f;

        [SerializeField, Tooltip("Sopra questa magnitudo di accelerazione istantanea (m/s^2), il delta velocita' viene considerato artefatto (snap di inversione/respawn/lancio skate) e NON alimenta la molla per quel frame. Default 30.")]
        private float snapVelocityDeltaIgnore = 30f;

        public Transform StackRoot => stackRoot;
        public int ItemCount => spawnedItems.Count;

        // Punteggio netto degli oggetti dall'ultimo reset: cresce ad ogni
        // raccolta (AddCollectedItem) e cala solo quando il kart NPC nemico
        // ruba oggetti (RemoveLastItems). NON cala per l'overflow della
        // torre (RemoveOldestItem) ne' per ClearAll: quelle non sono
        // perdite, solo gestione della pila visibile. E' il numero mostrato
        // nel Menu_Fine.
        private int totalCollected;
        public int TotalCollected => totalCollected;

        // Cache del kart: serve per leggere velocity/accelerazione ed urti.
        private ArcadeKart.Core.KartController kart;
        private Rigidbody kartRb;
        private bool wobbleReady;
        private float prevForwardSpeed;
        private float prevRightSpeed;
        private float prevKartYaw;
        private float wobbleDesyncTimer;

        // Low-pass sulle accelerazioni lette dal kart (F2 input smoothing):
        // invece di usare dFwd/dRight/yawVel grezzi alimentiamo la molla con
        // questi valori smorzati temporalmente, cosi' spike brevi (urto muro,
        // snap di inversione, sobbalzo) non fanno scattare la torre.
        private float smoothDFwd;
        private float smoothDRight;
        private float smoothYawVel;

        // Display layer per F1 (rate-limit visivo): per ogni oggetto abbiamo
        // pitch/roll "mostrati" che rincorrono i valori della molla ad un
        // massimo di maxWobbleDisplaySpeed gradi/sec. Esempio: la molla
        // vuole saltare di 60 gradi in un frame -> la torre visivamente sale
        // a 220 deg/sec, quindi arriva in ~0.27s = niente scatto.
        private readonly List<float> displayedPitch = new List<float>();
        private readonly List<float> displayedRoll = new List<float>();

        // Cooldown impatti (F3): ultimo tempo in cui abbiamo applicato un
        // impulso di urto. Ignoriamo altri urti entro impactCooldown secondi.
        private float lastImpactTime = -999f;

        private void Awake()
        {
            kart = GetComponentInParent<ArcadeKart.Core.KartController>();
            if (kart != null)
                kartRb = kart.GetComponent<Rigidbody>();

            wobbleReady = kartRb != null;
            if (wobbleReady)
            {
                Vector3 lv = kartRb.transform.InverseTransformDirection(kartRb.linearVelocity);
                prevForwardSpeed = lv.z;
                prevRightSpeed = lv.x;
                prevKartYaw = kartRb.transform.eulerAngles.y;
            }

            // Reset del contatore totale all'avvio: cosi' una ricarica di
            // scena (riavvio partita) riporta il punteggio a zero in modo
            // pulito, senza dipendere da logiche esterne. Per un reset
            // manuale a meta' partita (es. trigger di checkpoint) usare
            // ResetTotal().
            totalCollected = 0;
        }

        // Azzera il contatore totale raccolti. Pensato per essere chiamato
        // da trigger/zone di reset futuri (es. checkpoint, nuovo livello,
        // game over -> restart). non tocca la torre visibile: per quello
        // usare ClearAll().
        public void ResetTotal()
        {
            totalCollected = 0;
        }

        private void OnEnable()
        {
            if (kart != null)
                kart.OnImpact.AddListener(OnKartImpact);
        }

        private void OnDisable()
        {
            if (kart != null)
                kart.OnImpact.RemoveListener(OnKartImpact);
        }

        // KartController.OnImpact riporta la magnitudo dell'urto (m/s).
        // Lo trasformiamo in un impulso angolare distribuito con lever arm:
        // la cima della torre salta di piu' del fondo, come una pila di roba
        // che oscilla quando il carrello prende una botta.
        //
        // F3: gating + cooldown. Soglia minima perche' i piccoli sobbalzi di
        // una rampa bumposa (4-6 m/s) non siano urlati dalla torre; e cooldown
        // perche' anche sopra soglia, su una rampa a piu' sezioni, arrivano
        // 5+ impatti/sec e la torre finisce per impazzire martellata.
        private void OnKartImpact(float magnitude)
        {
            if (!wobbleReady || spawnedItems.Count == 0)
                return;

            if (impactMinMagnitude > 0f && magnitude < impactMinMagnitude)
                return;

            if (impactCooldown > 0f && (Time.time - lastImpactTime) < impactCooldown)
                return;
            lastImpactTime = Time.time;

            float strength = Mathf.Max(0f, magnitude);
            for (int i = 0; i < wobble.Count; i++)
            {
                float lever = 1f + leverPerIndex * i;
                ItemWobble w = wobble[i];
                // pitch verso avanti (come una frenata brusca): urto frontale.
                w.pitchVel += impactImpulse * lever * strength;
                // roll random: l'urto raramente e' perfettamente frontale,
                // aggiungiamo una componente laterale casuale.
                w.rollVel += UnityEngine.Random.Range(-1f, 1f) * impactLateralRandom * lever * strength;
                wobble[i] = w;
            }
        }

        private void Update()
        {
            // Sincronizza la lunghezza della lista wobble con spawnedItems:
            // e' una difensiva contro stati desincronizzati (oggetti distrutti
            // fuori band, add/remove che ho dimenticato di hookare, ecc.).
            wobbleDesyncTimer -= Time.deltaTime;
            if (wobbleDesyncTimer <= 0f)
            {
                SyncWobbleList();
                wobbleDesyncTimer = 1f;
            }

            if (!wobbleReady || spawnedItems.Count == 0 || kartRb == null)
                return;

            // Accel/moto del kart lette come delta della velocity locale del
            // rigidbody: fwdSpeed = forward (Z locale), rightSpeed = laterale (X).
            Vector3 localVel = kartRb.transform.InverseTransformDirection(kartRb.linearVelocity);
            float fwdSpeed = localVel.z;
            float rightSpeed = localVel.x;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            float rawDFwd = (fwdSpeed - prevForwardSpeed) / dt;
            float rawDRight = (rightSpeed - prevRightSpeed) / dt;
            prevForwardSpeed = fwdSpeed;
            prevRightSpeed = rightSpeed;

            // Yaw rate (deg/sec): usato per sentire le sterzate anche quando
            // il delta di velocita' laterale e' piccolo (curva a velocita'
            // costante: la torre sente la forza centrifuga).
            float curYaw = kartRb.transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(prevKartYaw, curYaw);
            prevKartYaw = curYaw;
            float rawYawVel = yawDelta / dt;

            // F5: snap gate. Se il delta velocita' istantaneo supera la
            // soglia, consideriamo il sample artefatto (snap di inversione
            // / respawn / lancio skate) e NON alimentiamo la molla per
            // questo frame. Ricalcoliamo i prev per il frame successivo
            // partendo dallo stato attuale, cosi' non compara un megakick
            // invertito al frame dopo.
            bool snapThisFrame =
                snapVelocityDeltaIgnore > 0f
                && (Mathf.Abs(rawDFwd) > snapVelocityDeltaIgnore
                    || Mathf.Abs(rawDRight) > snapVelocityDeltaIgnore);
            if (snapThisFrame)
            {
                rawDFwd = 0f;
                rawDRight = 0f;
            }

            // Clamp difensivo anti-spike residui.
            rawDFwd = Mathf.Clamp(rawDFwd, -60f, 60f);
            rawDRight = Mathf.Clamp(rawDRight, -60f, 60f);

            // F2: low-pass (EMA) sulle accelerazioni che alimentano la
            // molla. Costante di tempo inputSmoothing: a = 1 - exp(-dt/tau).
            // Questo attenua i transitori brevi (urto muro, sobbalzo di
            // rampa) senza intaccare l'accelerazione sostenuta (lean statico
            // in frenata/accelerazione continua), che e' lento-confronto.
            float alpha = 1f;
            if (inputSmoothing > 0f)
                alpha = 1f - Mathf.Exp(-dt / inputSmoothing);

            smoothDFwd = Mathf.Lerp(smoothDFwd, rawDFwd, alpha);
            smoothDRight = Mathf.Lerp(smoothDRight, rawDRight, alpha);
            smoothYawVel = Mathf.Lerp(smoothYawVel, rawYawVel, alpha);

            // Valori "puliti" che useremo per la molla.
            float dFwd = smoothDFwd;
            float dRight = smoothDRight;
            float yawVel = smoothYawVel;

            // ===== Catena cinematica bottom->top =====
            // La torre si piega come un corpo unico, non come oggetti che
            // ruotano sul posto. Ogni segmento eredita l'inclinazione di
            // tutti quelli sotto di lui: accum accumula la rotazione dal
            // fondo verso la cima, e pos accumula lo spostamento del
            // "giunto" superiore di ogni segmento ruotato. Cio' fa si' che
            // la cima della torre si sposti davvero quando la pila si piega
            // (effetto canna flessibile / carrello della spesa reale).
            //
            // La base rigida (i < rigidBaseCount) ha seg = identity: resta
            // dritta e ferma a baseLocalOffset, il piegamento comincia
            // sopra l'ultimo item rigido.
            //
            // F1: i valori applicati NON sono i raw wobble.pitch/roll della
            // molla, ma i "mostrati" (displayed) che rincorrono i raw ad
            // un massimo di maxWobbleDisplaySpeed gradi/sec. La catena si
            // costruisce sui displayed, quindi anche se la molla vuole
            // saltare di 60 gradi in un frame, la cima raggiungera' la
            // sua posa a velocita' uniforme -> niente scatto visivo.
            //
            // Caso statico (wobble tutto 0): accum resta identity e pos ==
            // baseLocalOffset + Vector3.up*verticalSpacing*i, identico a
            // RefreshStackLayout -> nessun pop visivo al passaggio.
            Quaternion baseRot = Quaternion.Euler(localEuler);
            Vector3 pos = baseLocalOffset;
            Quaternion accum = Quaternion.identity;
            int count = spawnedItems.Count;

            // Limite angolare di step del display layer (F1): deg femmax per
            // questo frame. 0 = rate-limit disattivato (seguia raw molla).
            float maxDisplayStep = (maxWobbleDisplaySpeed > 0f)
                ? maxWobbleDisplaySpeed * dt
                : float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (spawnedItems[i] == null)
                {
                    // Saltiamo l'oggetto nullo: la catena NON accumula per
                    // lui (preserva costeggiatura dei pezzi ancora vivi).
                    continue;
                }

                ItemWobble w = wobble[i];

                // Base rigida: azzera wobble e velocita' (pulizia residui).
                if (rigidBaseCount > 0 && i < rigidBaseCount)
                {
                    w.pitch = 0f;
                    w.roll = 0f;
                    w.pitchVel = 0f;
                    w.rollVel = 0f;
                    wobble[i] = w;
                }
                else
                {
                    // Lever arm differenziato: gli elementi in cima hanno
                    // braccio maggiore, oscillano piu' intensamente.
                    float lever = 1f + leverPerIndex * i;

                    // Segni:
                    //  - accel forward (dFwd > 0) -> piega INDIETRO -> pitch neg.
                    //  - frenata (dFwd < 0)      -> piega AVANTI   -> pitch pos.
                    //  - accel a destra (dRight > 0) per inerzia piega a sx -> roll neg.
                    //  - curva a destra (yawVel > 0) forza centrifuga a sx -> roll neg.
                    float pitchForce = -dFwd * longAccelToPitch * lever;
                    float rollForce = (-dRight * latAccelToRoll - yawVel * yawToRoll) * lever;

                    // E1 - Wind lean: a velocita' costante la torre resta
                    // inclinata all'indietro proporzionalmente alla velocita'
                    // forward, come se l'aria premesse sulla cima. Senza
                    // questo termine la torre in autostrada dritta sarebbe
                    // perfettamente verticale (irrealistico per una pila di
                    // roba su un carrello in corsa).
                    pitchForce -= fwdSpeed * windToPitch * lever;

                    // E2 - Micro-vibrazione: anche a velocita' costante la
                    // torre "vive" un po', scalza dall'irregolarita' del
                    // terreno. Due sinusoidi sfasate su pitch e roll per non
                    // sembrare un metronomo. Ampiezza scalata da velocity.
                    float vibSpeedRatio = (vibrationSpeedScale > 0f)
                        ? Mathf.Clamp01(Mathf.Abs(fwdSpeed) / vibrationSpeedScale)
                        : (Mathf.Abs(fwdSpeed) > 0f ? 1f : 0f);
                    float vibAmpDeg = vibrationAmplitude * vibSpeedRatio * lever;

                    if (vibAmpDeg > 0f)
                    {
                        float phase = Time.time * vibrationFrequency * Mathf.PI * 2f;
                        pitchForce += Mathf.Sin(phase) * vibAmpDeg;
                        // 1.7Hz.roll: stessa freq ma fase spostata, simula strada disomogenea.
                        rollForce += Mathf.Sin(phase * 1.7f + 1.3f) * vibAmpDeg * 0.7f;
                    }

                    // Molla smorzata. Smorzamento extra a riposo per
                    // uccidere il jitter numerico quanto piu' ferma possibile.
                    bool atRest = Mathf.Abs(pitchForce) < 1f && Mathf.Abs(rollForce) < 1f;
                    float damp = wobbleDamping + (atRest ? restDampingBoost : 0f);

                    w.pitchVel += (pitchForce - wobbleStiffness * w.pitch - damp * w.pitchVel) * dt;
                    w.rollVel += (rollForce - wobbleStiffness * w.roll - damp * w.rollVel) * dt;
                    w.pitch += w.pitchVel * dt;
                    w.roll += w.rollVel * dt;

                    // Clamp angolare con annullamento velocita' in uscita:
                    // evita accumulo di energia oltre il limite visivo.
                    if (maxWobbleAngle > 0f)
                    {
                        if (w.pitch > maxWobbleAngle) { w.pitch = maxWobbleAngle; if (w.pitchVel > 0f) w.pitchVel = 0f; }
                        else if (w.pitch < -maxWobbleAngle) { w.pitch = -maxWobbleAngle; if (w.pitchVel < 0f) w.pitchVel = 0f; }
                        if (w.roll > maxWobbleAngle) { w.roll = maxWobbleAngle; if (w.rollVel > 0f) w.rollVel = 0f; }
                        else if (w.roll < -maxWobbleAngle) { w.roll = -maxWobbleAngle; if (w.rollVel < 0f) w.rollVel = 0f; }
                    }

                    wobble[i] = w;
                }

                // F1: display layer. I valori mostrati rincorrono i raw
                // della molla ad un massimo di maxDisplayStep gradi per
                // frame. Tra un frame e l'altro possiamo muoverci ad esempio
                // di 220*0.016 = ~3.5 gradi massimo: la torre non scatta.
                float dispPitch = displayedPitch[i];
                float dispRoll = displayedRoll[i];
                dispPitch = Mathf.MoveTowards(dispPitch, w.pitch, maxDisplayStep);
                dispRoll = Mathf.MoveTowards(dispRoll, w.roll, maxDisplayStep);
                displayedPitch[i] = dispPitch;
                displayedRoll[i] = dispRoll;

                // Rotazione di questo singolo segmento. Identity se rigido.
                Quaternion seg = Quaternion.Euler(dispPitch, 0f, dispRoll);

                // Ereditiamo l'inclinazione di tutti i segmenti sotto: la
                // composizione accum*seg produce il tilt cumulativo della
                // cima rispetto alla base. Cio' che distingue una pila che
                // ruota sul posto da una che si piega come corpo e' questo
                // accumulo + lo spostamento posizionale del giunto sotto.
                accum = accum * seg;

                Transform t = spawnedItems[i].transform;
                t.localRotation = baseRot * accum;
                t.localPosition = pos;

                // Prossimo giunto: saliamo lungo la direzione "su" della
                // catena piegata. Usiamo accum (senza baseRot) perche' lo
                // spacing verticale appartiene allo spazio della torre, non
                // alla rotazione voluta dall'utente nel singolo pezzo.
                pos += accum * (Vector3.up * verticalSpacing);
            }

            // Evita che l'accelerazione derivata esploda il frame successivo
            // se l'oggetto e' stato disattivato/riattivato (Time.deltaTime 0).
            if (Time.timeScale <= 0f)
            {
                prevForwardSpeed = fwdSpeed;
                prevRightSpeed = rightSpeed;
            }
        }

        // Mantiene la lista wobble alla stessa lunghezza di spawnedItems,
        // reintegrando entry mancanti allo zero ed eliminando i ridondanti.
        private void SyncWobbleList()
        {
            while (wobble.Count < spawnedItems.Count)
            {
                wobble.Add(default);
                displayedPitch.Add(0f);
                displayedRoll.Add(0f);
            }
            while (wobble.Count > spawnedItems.Count)
            {
                wobble.RemoveAt(wobble.Count - 1);
                displayedPitch.RemoveAt(displayedPitch.Count - 1);
                displayedRoll.RemoveAt(displayedRoll.Count - 1);
            }
        }

        public void AddCollectedItem(string visualType)
        {
            if (string.IsNullOrWhiteSpace(visualType))
            {
                Debug.LogWarning("[KartCollectedStack] visualType vuoto.", this);
                return;
            }

            if (stackRoot == null)
            {
                Debug.LogWarning("[KartCollectedStack] Stack Root non assegnato.", this);
                return;
            }

            VisualEntry entry = FindEntry(visualType);
            if (entry == null)
            {
                Debug.LogWarning("[KartCollectedStack] Nessuna VisualEntry per il tipo: " + visualType, this);
                return;
            }

            // visualPrefab puo' essere "null" anche se la entry esiste: capita
            // quando il campo punta a un'istanza di scena anziche' a un prefab
            // asset di progetto e quella istanza viene distrutta a runtime
            // (es. e' essa stessa un Pickup raccolto e Destroy-ato).
            if (entry.visualPrefab == null)
            {
                Debug.LogWarning("[KartCollectedStack] Prefab visivo mancante o distrutto per il tipo: " + visualType + ". Assegna nel visualPrefab un prefab ASSET di progetto, non un'istanza di scena.", this);
                return;
            }

            if (maxItems > 0 && spawnedItems.Count >= maxItems)
                RemoveOldestItem();

            GameObject item = Instantiate(entry.visualPrefab, stackRoot);
            item.name = "Stack_" + visualType + "_" + Time.frameCount;

            SanitizeVisualClone(item);

            // Instantiate preserva lo stato attivo della sorgente: se il campo
            // visualPrefab punta a un'istanza di scena disattivata (es. essa
            // stessa un Pickup gia' raccolto e messo in attesa di respawn) il
            // clone nascerrebbe invisibile e il tipo sparirebbe dalla torre
            // pur occupando uno slot. Forziamo il clone attivo: e' grafica
            // pura, deve essere sempre visibile.
            if (!item.activeSelf)
                item.SetActive(true);

            Transform t = item.transform;
            t.localRotation = Quaternion.Euler(localEuler);
            t.localScale = entry.localScale;

            spawnedItems.Add(item);
            wobble.Add(default);
            displayedPitch.Add(0f);
            displayedRoll.Add(0f);
            RefreshStackLayout();

            // Punteggio netto: cresce ad ogni raccolta effettuata. Non viene
            // decrementato quando RemoveOldestItem scarta un elemento per
            // overflow del stack visibile ne' da ClearAll: rappresenta "quanti
            // ne ho presi e non mi sono fatti rubare dall'ultimo reset", non
            // "quanti ne ho addosso ora" (quello e' ItemCount).
            totalCollected++;
        }

        public void ClearAll()
        {
            for (int i = spawnedItems.Count - 1; i >= 0; i--)
            {
                if (spawnedItems[i] != null)
                    Destroy(spawnedItems[i]);
            }

            spawnedItems.Clear();
            wobble.Clear();
            displayedPitch.Clear();
            displayedRoll.Clear();
        }

        private void RemoveOldestItem()
        {
            if (spawnedItems.Count == 0)
                return;

            GameObject oldest = spawnedItems[0];
            spawnedItems.RemoveAt(0);
            if (wobble.Count > 0)
                wobble.RemoveAt(0);
            if (displayedPitch.Count > 0)
                displayedPitch.RemoveAt(0);
            if (displayedRoll.Count > 0)
                displayedRoll.RemoveAt(0);

            if (oldest != null)
                Destroy(oldest);
        }

        // Rimuove gli ultimi N oggetti raccolti (dalla CIMA della torre,
        // cioe' i piu' recenti). Usato dal kart NPC nemico (EnemyKart) quando
        // entra in contatto fisico col giocatore: gli fa "cadere" gli
        // ultimi oggetti presi nel livello. Distrugge i cloni visivi senza
        // rilasciarli nel mondo (coerente con RemoveOldestItem, che fa lo
        // stesso per l'overflow del stack visibile). Se count eccede
        // ItemCount, li toglie tutti. Decrementa anche totalCollected:
        // e' un FURTO, una vera perdita per il giocatore, quindi il
        // punteggio netto (mostrato nel Menu_Fine) deve calare. Attenzione:
        // RemoveOldestItem (overflow della pila) NON decrementa, perche'
        // quello non e' una perdita di punteggio, solo visiva.
        public void RemoveLastItems(int count)
        {
            if (count <= 0 || spawnedItems.Count == 0)
                return;

            int toRemove = Mathf.Min(count, spawnedItems.Count);

            for (int i = 0; i < toRemove; i++)
            {
                int last = spawnedItems.Count - 1;
                GameObject item = spawnedItems[last];
                spawnedItems.RemoveAt(last);

                // Manteniamo le liste wobble/displayed allineate a
                // spawnedItems togliendo dalla stessa estremita' (cima).
                if (wobble.Count > 0)
                    wobble.RemoveAt(wobble.Count - 1);
                if (displayedPitch.Count > 0)
                    displayedPitch.RemoveAt(displayedPitch.Count - 1);
                if (displayedRoll.Count > 0)
                    displayedRoll.RemoveAt(displayedRoll.Count - 1);

                if (item != null)
                    Destroy(item);
            }

            // Punteggio netto: gli oggetti rubati dal NPC scendono dal
            // contatore. Max(0, ...) solo per sicurezza: teoricamente la
            // torre non puo' contenere piu' di quanto totalCollected
            // conteggi, quindi non puo' andare in negativo da solo.
            totalCollected = Mathf.Max(0, totalCollected - toRemove);

            RefreshStackLayout();
        }

        private void RefreshStackLayout()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] == null)
                    continue;

                Transform t = spawnedItems[i].transform;
                t.localPosition = baseLocalOffset + Vector3.up * (verticalSpacing * i);
                t.localRotation = Quaternion.Euler(localEuler);
            }
        }

        private VisualEntry FindEntry(string visualType)
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                if (string.Equals(visuals[i].visualType, visualType, StringComparison.OrdinalIgnoreCase))
                    return visuals[i];
            }

            return null;
        }

        // I cloni della torre esistono solo per scopi visivi: non devono
        // partecipare alla fisica ne' riusare la logica di Pickup del
        // prefab sorgente (che e' pensata per l'oggetto "vivo" in scena).
        // Ripuliamo quindi ogni clone a runtime cosi' e' sicuro per
        // costruzione, indipendentemente da come e' configurato il prefab
        // assegnato nel campo visualPrefab (trigger, non-trigger, con
        // Rigidbody, con Pickup, su layer sbagliato...). Cosi' una torre
        // alta non puo' mai bloccare il kart in tunnel/passaggi stretti.
        private void SanitizeVisualClone(GameObject item)
        {
            // Layer inerte: non collide col kart ne' coi suoi ground check.
            foreach (Transform tr in item.GetComponentsInChildren<Transform>(true))
                tr.gameObject.layer = stackLayer;

            // Disattiviamo ogni collider: il clone non genera ne' contatti
            // fisici ne' eventi trigger. Include eventuali collider
            // disabilitati di default nel prefab (true li prende comunque).
            Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            // Rimuoviamo eventuali Rigidbody: senza corpo fisico il clone
            // e' grafica pura, non puo' oscillare/spingere/cadere.
            Rigidbody[] rbs = item.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null)
                    Destroy(rbs[i]);
            }

            // Rimuoviamo il componente Pickup: e' la logica di raccolta
            // dell'oggetto "vivo" e non deve girare sui cloni della torre
            // (altrimenti ogni elemento impilato tenterebbe di raccogliere
            // ancora il player e sprecerebbe callback ad ogni passaggio).
            Pickup[] pickups = item.GetComponentsInChildren<Pickup>(true);
            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null)
                    Destroy(pickups[i]);
            }

            // Nascondiamo lo sprite billboard animato (figlio "Sprite" del
            // prefab): in scena serve all'oggetto vivo come icona che guarda
            // la camera, ma sulla torre sarebbe solo rumore visivo (una fila
            // di anelli animati), quindi disattiviamo il suo GameObject.
            // SetActive(false) sul pivot spegne anche il quadro figlio:
            // niente SpriteRenderer, niente billboard, niente animazione.
            // Se il prefab assegnato non ha un figlio con quel nome non
            // succede niente, quindi qualunque visualPrefab resta sicuro.
            Transform spritePivot = item.transform.Find("Sprite");
            if (spritePivot != null)
                spritePivot.gameObject.SetActive(false);
        }
    }
}
