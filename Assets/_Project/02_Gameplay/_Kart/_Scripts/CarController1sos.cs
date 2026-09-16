using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ArcadeKart.Core
{
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : MonoBehaviour
    {
        #region Inspector

        [Header("Speed")]
        [SerializeField, Tooltip("Velocita' massima in unita'/secondo.")]
        private float maxSpeed = 22f;

        [SerializeField, Tooltip("Velocita' massima di crociera quando NON tieni premuto il tasto boost (mouse sx). Con boost (mouse sx) si raggiunge maxSpeed.")]
        private float cruiseSpeed = 12f;

        [SerializeField, Tooltip("Decelerazione (unita'/sec^2) con cui il soffitto di velocita' cala da maxSpeed a cruiseSpeed quando rilasci il boost (mouse sx). Più basso = transizione più morbida (niente taglio istantaneo).")]
        private float boostReleaseDeceleration = 8f;

        [SerializeField, Tooltip("Rate di decadimento dell'eccesso di cap del drift-boost (mini-turbo) quando il multiplier scade. Esponenziale: piu' alto = il cap torna al normale piu' in fretta dopo il drift-boost. Robusto a qualsiasi activeDriftBoostMagnitude.")]
        private float driftBoostEndDecay = 6f;

        [SerializeField, Tooltip("Accelerazione (unita'/sec^2).")]
        private float acceleration = 14f;

        [SerializeField, Tooltip("Decelerazione quando rilasci il gas o freni.")]
        private float deceleration = 10f;

        [SerializeField, Tooltip("Velocita' massima in retromarcia.")]
        private float reverseSpeed = 6f;

        [SerializeField, Tooltip("Forza extra del freno quando il giocatore preme Brake.")]
        private float brakeStrength = 18f;

        // ===== Steering: stesso schema per-fase del Grip / Drift =====
        // Due set (CORSA con i nomi storici agganciati ai valori di scena,
        // CAMMINATA con i gemelli) fusi a runtime con PesoCorsa. Le variabili
        // "assolute" (turnRate, turnAtRest, cameraRelativeTurnResponsiveness)
        // sono quelle che cambiano davvero comportamento a soffitto basso;
        // shoppingCartSlip/SteerLoss sono gia' normalizzate su speedRatio e
        // qui vengono duplicate solo per liberta' di ritratura extra.
        [Header("Steering — Corsa (mouse sx tenuto)")]
        [SerializeField, Tooltip("Gradi al secondo di sterzata a velocita' massima, in fase CORSA.")]
        private float turnRate = 400f;

        [SerializeField, Tooltip("Quanto si gira da fermo in fase CORSA (0 = niente, 1 = come a piena velocita').")]
        [Range(0f, 1f)]
        private float turnAtRest = 0.72f;

        [SerializeField, Tooltip("Quanto la sterzata perde efficacia alle alte velocita', in fase CORSA. 0 = sterzo sempre uguale, 1 = molto effetto carrello.")]
        [Range(0f, 1f)]
        private float shoppingCartSteerLoss = 0.65f;

        [SerializeField, Tooltip("Quanto il kart perde grip laterale in curva alle alte velocita', in fase CORSA.")]
        [Range(0f, 1f)]
        private float shoppingCartSlip = 0.75f;

        [SerializeField, Tooltip("Quanto input di sterzo serve per iniziare a far slittare sensibilmente il kart, in fase CORSA.")]
        [Range(0f, 1f)]
        private float shoppingCartSlipSteerThreshold = 0.15f;

        [SerializeField, Tooltip("Velocita' con cui il kart ruota verso la direzione desiderata letta dalla camera, in fase CORSA (reorientation).")]
        private float cameraRelativeTurnResponsiveness = 10f;

        [Header("Steering — Camminata (boost rilasciato)")]
        [SerializeField, Tooltip("Gradi al secondo di sterzata a velocita' massima di fase, in fase CAMMINATA (boost rilasciato). Si fonde dolcemente con la versione corsa durante la transizione. Default = valore corsa.")]
        private float turnRateCamminata = 400f;

        [SerializeField, Tooltip("Quanto si gira da fermo in fase CAMMINATA (0 = niente, 1 = come a piena velocita' di fase). Default = valore corsa.")]
        [Range(0f, 1f)]
        private float turnAtRestCamminata = 0.72f;

        [SerializeField, Tooltip("Quanto la sterzata perde efficacia alle alte velocita' di fase, in fase CAMMINATA. Default = valore corsa.")]
        [Range(0f, 1f)]
        private float shoppingCartSteerLossCamminata = 0.65f;

        [SerializeField, Tooltip("Quanto il kart perde grip laterale in curva alle alte velocita' di fase, in fase CAMMINATA. Default = valore corsa.")]
        [Range(0f, 1f)]
        private float shoppingCartSlipCamminata = 0.75f;

        [SerializeField, Tooltip("Quanto input di sterzo serve per iniziare a far slittare sensibilmente il kart, in fase CAMMINATA. Default = valore corsa.")]
        [Range(0f, 1f)]
        private float shoppingCartSlipSteerThresholdCamminata = 0.15f;

        [SerializeField, Tooltip("Velocita' con cui il kart ruota verso la direzione desiderata letta dalla camera, in fase CAMMINATA (reorientation). Default = valore corsa.")]
        private float cameraRelativeTurnResponsivenessCamminata = 10f;

        [Header("Reorientation")]
        [SerializeField, Tooltip("Soglia angolare (gradi) per il cambio direzione istantaneo. Sopra questo angolo il kart scatta subito verso la nuova direzione preservando la spinta longitudinale; sotto usa la sterzata graduale con drift e slip 'carrello della spesa'. 360 = mai snap (comportamento originale).")]
        [Range(0f, 360f)]
        private float instantRealignAngle = 90f;

        [SerializeField, Tooltip("Quanta della spinta longitudinale	vecchia viene reindirizzata lungo il nuovo forward durante uno snap. 1 = cambio direzione pulito, nessun residuo; <1 = parte della velocita' resta nel verso di prima e decresce naturalmente con il grip (derapata post-snap stile 'carrello della spesa').")]
        [Range(0f, 1f)]
        private float instantRealignLongitudinalRetention = 0.6f;

        [SerializeField, Tooltip("Sotto questa velocita' il kart e' considerato quasi fermo.")]
        private float rotateBeforeMoveSpeedThreshold = 0.35f;

        [SerializeField, Tooltip("Se il kart e' quasi fermo, deve prima girarsi sotto questo angolo per poter accelerare.")]
        private float rotateBeforeMoveReleaseAngle = 10f;

        [SerializeField, Tooltip("Angolo oltre il quale, in corsa, il cambio direzione viene trattato come inversione forte.")]
        private float movingReorientationEnterAngle = 135f;

        [SerializeField, Tooltip("Quando l'angolo scende sotto questo valore, il kart torna a spingere nella nuova direzione.")]
        private float movingReorientationExitAngle = 22f;

        [SerializeField, Tooltip("Velocita' planare minima per attivare la reorientation mentre sei in corsa.")]
        private float movingReorientationMinSpeed = 4f;

        [SerializeField, Tooltip("Quanto viene ridotta la nuova accelerazione mentre il kart si sta riallineando in corsa. 0 = nessuna spinta nuova.")]
        [Range(0f, 1f)]
        private float movingReorientationAccelerationFactor = 0.05f;

        [SerializeField, Tooltip("Extra frenata sul forward locale mentre il kart si riallinea in corsa.")]
        private float movingReorientationBrakeStrength = 20f;

        // ===== Grip / Drift: le variabili esistono in DUE set, uno per fase =====
        // CORSA (boost mouse sx tenuto) e CAMMINATA (boost rilasciato). I valori
        // CORSA sono quelli sintonizzati storicamente (quando il kart era
        // permanentemente in corsa e i nomi dei campi non sono cambiati, cosi'
        // i valori gia' serializzati nelle scene restano agganciati). I valori
        // CAMMINATA partono identici ai corsa per non alterare il feeling di
        // partenza: si ritunano solo per la fase bassa. A runtime i due set si
        // fondono con PesoCorsa (vedi region Internal), derivato dal soffitto
        // smorzato currentEffectiveMax: cosi' il grip "da corsa" resta finche'
        // il soffitto e' alto e scivola su quello "da camminata" durante il
        // rilascio del boost, senza scatti.
        [Header("Grip / Drift — Corsa (mouse sx tenuto)")]
        [SerializeField, Tooltip("Grip laterale normale a terra in fase CORSA. Alto = il kart si riallinea meglio.")]
        private float groundLateralFriction = 10f;

        [SerializeField, Tooltip("Grip laterale mentre sei in aria in fase CORSA. Basso = mantiene piu' inerzia laterale.")]
        private float airLateralFriction = 100f;

        [SerializeField, Tooltip("Grip laterale mentre tieni premuto Drift, in fase CORSA.")]
        private float driftLateralFriction = 4f;

        [SerializeField, Tooltip("Velocita' minima del kart per considerare attivo il drift, in fase CORSA.")]
        private float driftMinSpeed = 4f;

        [SerializeField, Tooltip("Input minimo di direzione per considerare attivo il drift, in fase CORSA.")]
        [Range(0f, 1f)]
        private float driftMinSteer = 0.2f;

        [SerializeField, Tooltip("Moltiplicatore di sterzata mentre tieni Drift, in fase CORSA.")]
        private float driftSteerBoost = 1.4f;

        [SerializeField, Tooltip("Transform del mesh visivo del kart (comune alle due fasi).")]
        private Transform driftVisual;

        [SerializeField, Tooltip("Gradi massimi di rotazione visiva del mesh durante il drift, in fase CORSA.")]
        private float driftVisualYawDegrees = 25f;

        [SerializeField, Tooltip("Velocita' di transizione dello yaw visivo, in fase CORSA.")]
        private float driftVisualLerpSpeed = 8f;

        [Header("Grip / Drift — Camminata (boost rilasciato)")]
        [SerializeField, Tooltip("Grip laterale normale a terra in fase CAMMINATA (boost rilasciato). Si fonde dolcemente con la versione corsa durante la transizione. Default = valore corsa: regolarlo per la fase bassa.")]
        private float groundLateralFrictionCamminata = 10f;

        [SerializeField, Tooltip("Grip laterale mentre sei in aria in fase CAMMINATA. Basso = mantiene piu' inerzia laterale. Default = valore corsa.")]
        private float airLateralFrictionCamminata = 100f;

        [SerializeField, Tooltip("Grip laterale mentre tieni premuto Drift, in fase CAMMINATA. Default = valore corsa.")]
        private float driftLateralFrictionCamminata = 4f;

        [SerializeField, Tooltip("Velocita' minima del kart per considerare attivo il drift, in fase CAMMINATA. Attenzione: se supera cruiseSpeed il drift passivo non puo' MAI attivarsi in camminata (es. 4 con cruise 3). Default = valore corsa.")]
        private float driftMinSpeedCamminata = 4f;

        [SerializeField, Tooltip("Input minimo di direzione per considerare attivo il drift, in fase CAMMINATA. Default = valore corsa.")]
        [Range(0f, 1f)]
        private float driftMinSteerCamminata = 0.2f;

        [SerializeField, Tooltip("Moltiplicatore di sterzata mentre tieni Drift, in fase CAMMINATA. Default = valore corsa.")]
        private float driftSteerBoostCamminata = 1.4f;

        [SerializeField, Tooltip("Gradi massimi di rotazione visiva del mesh durante il drift, in fase CAMMINATA. Default = valore corsa.")]
        private float driftVisualYawDegreesCamminata = 25f;

        [SerializeField, Tooltip("Velocita' di transizione dello yaw visivo, in fase CAMMINATA. Default = valore corsa.")]
        private float driftVisualLerpSpeedCamminata = 8f;

        [Header("Active Drift")]
        [SerializeField, Tooltip("Il drift (attivo e passivo) e' abilitato solo se ALMENO UNO di questi oggetti e' attivo nella scena (activeInHierarchy). Attivando/disattivando un oggetto della lista (SetActive) si abilita/disabilita il drift. Lista vuota o tutti nulli = drift sempre disattivo.")]
        private List<GameObject> activeDriftToggleObjects;

        // Stato del gate drift: true se ALMENO UN oggetto della lista e'
        // assegnato e attivo nella gerarchia (activeInHierarchy copre anche
        // la disattivazione di un genitore). Lista vuota o tutti nulli =
        // drift disattivo.
        private bool ActiveDriftEnabled
        {
            get
            {
                if (activeDriftToggleObjects == null) return false;
                for (int i = 0; i < activeDriftToggleObjects.Count; i++)
                {
                    GameObject go = activeDriftToggleObjects[i];
                    if (go != null && go.activeInHierarchy) return true;
                }
                return false;
            }
        }

        [SerializeField, Tooltip("Velocita' planare minima del kart per attivare e mantenere il drift attivo (Shift + sterzo). Sotto questo valore (es. dopo un impatto col muro) il drift si interrompe, senza boost.")]
        private float activeDriftMinSpeed = 5f;

        [SerializeField, Tooltip("Input di sterzo laterale (Move.x) minimo per entrare nel drift attivo.")]
        [Range(0f, 1f)]
        private float activeDriftMinSteer = 0.35f;

        [SerializeField, Tooltip("Grip laterale durante il drift attivo (separato dal drift passivo). Basso = la velocity slitta rispetto al muso (derapata). Default basso (1.5) per far seguire la velocity al muso durante un 360: con grip 3 la velocity resta indietro e il drift appare lento.")]
        private float activeDriftLateralFriction = 1.5f;

        [SerializeField, Tooltip("Frazione della speed all'ingresso tenuta come velocita' longitudinale target durante la derapata. 1 = conserva, <1 = decelera leggermente. Clamp in ogni caso al floor (activeDriftMinForwardSpeed).")]
        [Range(0f, 1f)]
        private float activeDriftForwardRetention = 0.95f;

        [SerializeField, Tooltip("FLOOR PLANARE (unita'/sec) della velocity mondiale durante il drift attivo. Il kart non scende mai sotto questa magnitudine finche' resta in drift: sostiene la speed anche durante un 360 (la componente locale forward oscilla col muso, ma la velocity totale resta sopra il floor). Gated sul muro: durante un impatto (WallContactActive) il floor e' disattivato, cosi' l'urto abbassa planarSpeed e fa uscire il drift (vedi activeDriftMinSpeed). Deve essere >= ad activeDriftMinSpeed per evitare uscite per bassa velocita'.")]
        private float activeDriftMinForwardSpeed = 8f;

        [SerializeField, Tooltip("Tempo di tolleranza (sec) prima di uscire dal drift attivo per perdita di grounding. Bump brevi su curb/disconnessioni del terreno (IsGrounded=false per pochi frame) NON spezzano il drift. Solo dopo questo tempo di volo prolungato (es. salto vero, skate ramp) il drift si interrompe. Previene le false exit su lievi sbalzi di terreno durante una curva.")]
        private float activeDriftExitGraceTime = 0.10f;

        [SerializeField, Tooltip("Cap hard (gradi/sec) di quanto il muso puo' ruotare verso il joystick durante il drift attivo. Previene spin istantanei: niente 360 in 0.1 sec anche se il joystick fa cerchi completi. Indipendente dalla sterzata normale (turnRate).")]
        private float activeDriftMaxTurnRate = 150f;

        [SerializeField, Tooltip("Costante di stabilizzazione dello slip (gradi). Controlla quanto lo slip angle (muso vs velocity) deve crescere prima che la velocity acceleri verso il muso. Piu' basso = velocity segue prima (meno derapata, piu' reattivo). Piu' alto = velocity segue dopo (piu' derapata, piu' slittamento). La velocity ruota verso il muso a rate = grip * (1 + slip/K) * 57.3 gradi/sec. Con K=30 e grip=1: a 0 gradi slip = 57 gradi/sec, a 30 gradi = 115 gradi/sec, a 75 gradi = 200 gradi/sec (pari a maxTurnRate=200).")]
        private float activeDriftSlipStabilizeK = 30f;

        [SerializeField, Tooltip("Angolo minimo (gradi) fra muso del kart e direzione del joystick per accumulare carica boost. Sotto questa soglia (es. vai dritto) NON carichi. La carica accumulata resta pero' sticky (non decade): serve a impedire 'charge for free' andando dritto.")]
        private float activeDriftChargeMinAngle = 15f;

        [SerializeField, Tooltip("Tempo (sec) di sterzata sopra soglia richiesto per caricare completamente il boost (singola fase). Una volta raggiunto, isDriftCharged diventa true e resta sticky fino al rilascio del Shift o perdita speed.")]
        private float activeDriftChargeTime = 1.0f;

        [SerializeField, Tooltip("Velocita' di accumulo della carica (unita'/sec). Con driftChargeRate=1 e activeDriftChargeTime=1, ci vuole 1 secondo di sterzata sopra soglia per caricare.")]
        private float driftChargeRate = 1f;

        [SerializeField, Tooltip("Magnitude (moltiplicatore speed) del boost in uscita al rilascio dello Shift con carica completata. Singola fase: valore fisso.")]
        private float activeDriftBoostMagnitude = 1.5f;

        [SerializeField, Tooltip("Durata (sec) del boost in uscita al rilascio dello Shift con carica completata. Singola fase: valore fisso.")]
        private float activeDriftBoostDuration = 0.8f;

        [SerializeField, Tooltip("Kick istantaneo di velocita' in avanti (unita'/sec) applicato al rilascio del drift carico. A differenza del multiplier (graduale) questo si sente subito come uno 'scatto' in avanti. 0 = niente kick, solo multiplier. ~8 = scatto netto stile mini-turbo.")]
        private float activeDriftBoostKick = 8f;

        [SerializeField, Tooltip("Inclinazione visiva (yaw del mesh) durante il drift attivo, come frazione del drift passivo. 0 = nessuna inclinazione, 0.4 = lieve (40% del passivo), 1 = identica al passivo. Il muso segue il joystick, il kart resta sostanzialmente dritto.")]
        [Range(0f, 1f)]
        private float activeDriftVisualYawScale = 0.4f;

        [SerializeField, Tooltip("Velocita' di transizione dello yaw visivo durante il drift attivo (separato dal drift passivo). Default 12 = piu' veloce del passivo (8): la visual yaw torna a 0 rapidamente dopo un 360, niente 'rimane inclinato' residuo.")]
        private float activeDriftVisualLerpSpeed = 12f;

        [Header("Ground & Gravity")]
        [SerializeField, Tooltip("Gravita' custom applicata al kart.")]
        private float gravity = 30f;

        [SerializeField, Tooltip("Quanto risponde il kart in aria alla direzione desiderata (0-1).")]
        [Range(0f, 1f)]
        private float airControl = 0.3f;

        [SerializeField, Tooltip("Distanza massima dello SphereCast centrale per rilevare il terreno e la sospensione.")]
        private float groundCheckDistance = 1.2f;

        [SerializeField, Tooltip("Raggio dello SphereCast per il controllo del terreno.")]
        private float groundCheckRadius = 0.35f;

        [SerializeField, Tooltip("Punto di partenza dello SphereCast centrale.")]
        private Transform groundCheckOrigin;

        [SerializeField, Tooltip("Layer considerati come terreno. Imposta SOLO Ground.")]
        private LayerMask groundLayer;

        [SerializeField, Tooltip("Angolo massimo (gradi) della superficie considerata terreno. Oltre questo valore e' un muro: il kart non ci sale e scivola giu'.")]
        [Range(0f, 89f)]
        private float maxGroundSlopeAngle = 80f;

        [SerializeField, Tooltip("Piccolo tempo di tolleranza prima di perdere lo stato grounded.")]
        private float groundedGraceTime = 0.08f;

        [SerializeField, Tooltip("Altezza desiderata del kart dal terreno.")]
        private float rideHeight = 0.8f;

        [SerializeField, Tooltip("Forza della sospensione raycast.")]
        private float suspensionStrength = 90f;

        [SerializeField, Tooltip("Smorzamento della sospensione.")]
        private float suspensionDamping = 12f;

        [SerializeField, Tooltip("Alzata del fondo della/e CapsuleCollider fisiche del kart (metri, applicata a runtime restringendo la capsula e tenendo fisso il top). Con il fondo alzato, su terreno piatto il kart cavalca SOLO sulla sospensione raycast e non tocca mai i bordi di giunzione fra i collider (niente piu' ghost bump posizionale). Il contatto fisico si ristabilisce da solo dove serve: su pendenze (la superficie risale verso la capsula entro pochi gradi), agli atterraggi e sui lati (muri/pareti rampa, intatti). 0 = comportamento originale con contatto a riposo.")]
        [Range(0f, 0.15f)]
        private float groundContactClearance = 0.07f;

        [SerializeField, Tooltip("Smorzamento minimo della sospensione, applicato in Awake SOLO con capsula flottante (groundContactClearance > 0). Senza contatto a riposo il kart sta solo sulla molla: col damping basso della scena (0.1) oscillerebbe visibilmente (~1.5 Hz). 9 con strength 90 da rapporto di smorzamento ~0.47, assestamento pulito. Se la sospensione in scena e' piu' alta del minimo, resta quella.")]
        private float minSuspensionDamping = 9f;

        [SerializeField, Tooltip("Forza massima (accelerazione) con cui la sospensione estesa puo' tirare il kart VERSO il terreno (molla bidirezionale, sulla distanza perpendicolare al piano). ATTIVA solo dove il damping e' attivo (piatto e pendenze dolci): serve a far seguire le discese senza contatto fisico. Su pendenze riperte (dampScale = 0) la sospensione e' push-only come prima della capsula flottante. 0 = mai tirare verso il basso.")]
        private float suspensionMaxPullDown = 45f;

        [SerializeField, Tooltip("Velocita' verticale massima (unita'/sec) consentita verso l'alto quando il kart e' grounded su terreno ~piatto (normale Y del hit >= groundFlatNormalThreshold), oltre alla salita legittima stimata dalla pendenza. Rete di sicurezza per gli impulsi +Y residui (con la capsula flottante su piatto non arriva piu' alcun contatto, quindi resta quasi sempre silente). Non interviene in aria, in discesa, sulle pendenze piu' riperte della soglia (rampe) ne' durante il lancio skate.")]
        private float seamHopMaxVerticalSpeed = 0.6f;

        [SerializeField, Tooltip("Soglia di planarita' della normale del terreno (componente Y) per il filtro anti ghost-bump: sotto questa soglia (pendenze riperte, rampe) la salita e' considerata legittima e il filtro non interviene.")]
        [Range(0.5f, 1f)]
        private float groundFlatNormalThreshold = 0.95f;

        [Header("Wall Avoidance")]
        [SerializeField, Tooltip("Tempo di tolleranza in cui il contatto col muro resta attivo anche se la collisione sfarfalla.")]
        private float wallContactGraceTime = 0.2f;

        [Header("Air Stability")]
        [SerializeField, Tooltip("Smorza la rotazione residua in aria per evitare spin strani al rientro.")]
        private float airAngularDamping = 2.5f;

        [SerializeField, Tooltip("Limite massimo della velocita' angolare Y in aria.")]
        private float maxAirYawAngularVelocity = 2.5f;

        [SerializeField, Tooltip("Quanto smorzare la rotazione al momento dell'atterraggio.")]
        [Range(0f, 1f)]
        private float landingAngularDampingFactor = 0.2f;

        [Header("Ground Alignment Visual")]
        [SerializeField, Tooltip("Probe anteriore sinistra per allineamento visivo al terreno.")]
        private Transform frontLeftGroundProbe;

        [SerializeField, Tooltip("Probe anteriore destra per allineamento visivo al terreno.")]
        private Transform frontRightGroundProbe;

        [SerializeField, Tooltip("Probe posteriore sinistra per allineamento visivo al terreno.")]
        private Transform rearLeftGroundProbe;

        [SerializeField, Tooltip("Probe posteriore destra per allineamento visivo al terreno.")]
        private Transform rearRightGroundProbe;

        [SerializeField, Tooltip("Distanza dei raycast usati per inclinare visivamente il kart.")]
        private float visualGroundAlignDistance = 1.4f;

        [SerializeField, Tooltip("Velocita' di allineamento del mesh alla pendenza del terreno.")]
        private float groundAlignLerpSpeed = 10f;

        [SerializeField, Tooltip("Tempo (sec) con cui il modello rincorre la rotazione del corpo. Piu' alto = oscillazioni lunghe e fluide; piu' basso = modello incollato al corpo.")]
        private float visualYawSmoothTime = 0.15f;

        [SerializeField, Tooltip("Velocita' massima (gradi/sec) con cui il muso del modello ruota verso la direzione di sterzo. Il corpo fisico puo' scattare piu' in fretta (es. inversioni): il modello oscilla a questa velocita' costante invece di seguirlo.")]
        private float visualYawMaxTurnSpeed = 400f;

        [Header("Skate Ramp Launch")]
        [SerializeField, Tooltip("Velocita' angolare massima (gradi/sec) con cui il visual del kart si riallinea alla traiettoria parabolica durante un lancio skate. Basso = il muso segue lentamente la parabola (piu' fluido, meno 'snappy'); alto = il muso si allinea subito alla velocity. Simile a visualYawMaxTurnSpeed ma applicato al lancio skate.")]
        private float skateRampVisualTurnSpeed = 180f;

        [Header("Impact")]
        [SerializeField, Tooltip("Velocita' minima di urto per invocare OnImpact.")]
        private float impactThreshold = 5f;

        [SerializeField, Tooltip("Soglia sulla normale di contatto per classificare un urto come 'contro un muro': normale con |y| sotto questo valore = superficie verticale (muro o parete-rampa). 0.3 = circa 72 gradi o piu'. I contatti con collider di tipo groundLayer sono ESCLUSI (pareti-rampa skate: non contano come muri).")]
        [Range(0f, 1f)]
        private float sogliaNormaleMuro = 0.3f;

        [Header("AI / Sterzata costante")]
        [SerializeField, Tooltip("Modalita' per kart pilotati dalla CPU (NPC). Bypassa lo snap di inversione (instantRealignAngle), il gating 'ruota-prima-di-muoverti' (isReorientingFromStop) e la frenata/limitazione di reorientation in corsa (isReorientingWhileMoving), e usa sempre il turnRate pieno a qualsiasi velocita': il kart sterza COSTANTEMENTE verso la direzione desiderata e accelera sempre verso il target, senza scatti ne' blocchi. Lasciarlo false sul kart del giocatore. EnemyKart lo attiva in Awake.")]
        private bool aiSteeringMode = false;

        #endregion

        #region Events

        public UnityEvent<bool> OnGroundedChanged;
        public UnityEvent<float> OnSpeedChanged;
        public UnityEvent<float> OnImpact;

        // Invocato quando un urto sopra impactThreshold colpisce una
        // superficie verticale (muro/parete-rampa): la normale di contatto
        // e' orizzontale (|y| < sogliaNormaleMuro). Separato da OnImpact,
        // che scatta su qualsiasi urto forte (anche atterraggi duri).
        public UnityEvent<float> OnImpattoMuro;

        #endregion

        #region Public API

        public float CurrentSpeed { get; private set; }
        public float MaxSpeed => maxSpeed;

        // Peso della fase guida (0 = camminata, 1 = corsa) gia' smorzato dal
        // controller: e' lo stesso PesoCorsa interno con cui si fondono i set
        // di grip/sterzo/velocity. Esposto per chi deve seguire la fase da
        // fuori (es. AudioRuoteKart) senza duplicarne la logica.
        public float PesoCorsaFase => PesoCorsa;

        // Soffitto planare COMBINATO smorzato (cap * multiplier, vedi
        // currentPlanarMax). Esposto per la modulazione audio: il rapporto
        // velocita'/soffitto reagisce alla velocita' REALE (cali durante
        // inversioni/ralenti), oltre alla sola fase camminata/corsa.
        public float SoffittoVelocitaAttuale => currentPlanarMax;

        // True in tutta la fase lancio skate (contatto con parete verticale
        // + volo balistico fino all'atterraggio). Esposto per l'audio: durante
        // la parete IsGrounded resta vero (lo SphereCast becca la rampa sotto)
        // ma le ruote non toccano terra "camminabile".
        public bool LancioSkateAttivo => skateRampLaunch;

        // Modalita' AI: vedi tooltip di aiSteeringMode. Letta/scritta dal NPC
        // (EnemyKart) in Awake per attivare la sterzata costante. Pubblica
        // perche' deve essere leggibile anche da fuori (es. debug).
        public bool AiSteeringMode
        {
            get => aiSteeringMode;
            set => aiSteeringMode = value;
        }

        public bool IsGrounded { get; private set; }

        public bool IsDrifting =>
            IsDriftingActive
            || (ActiveDriftEnabled
                && CurrentDrift
                && IsGrounded
                && Mathf.Abs(CurrentSpeed) >= driftMinSpeedCorrente
                && CurrentMove.sqrMagnitude >= driftMinSteerCorrente * driftMinSteerCorrente);

        public bool IsDriftingActive => isDriftingActive;

        public float DriftCharge => driftCharge;

        public bool IsDriftCharged => isDriftCharged;

        // True durante la finestra del mini-turbo di uscita dal drift carico
        // (ApplyBoost: multiplier attivo con valore > 1). ApplySlow usa valori
        // < 1 quindi non conta. Derivato dallo stato del multiplier coroutine:
        // niente timer dedicato da mantenere in sync.
        public bool IsDriftBoosting =>
            multiplierRoutine != null && speedMultiplier > 1f;

        // True quando il kart "sgomma" VISIVAMENTE (per KartSkidmarks): drift
        // attivo, oppure drift passivo (Shift tenuto) MA non durante la
        // finestra del boost-reward del drift. In quel caso i trail si spengono
        // (stai boostando, non sgommando). Separato da IsDrifting per non
        // alterare la fisica: IsDrifting resta usato dalla grip laterale.
        public bool IsSkidding =>
            IsDriftingActive || (IsDrifting && !IsDriftBoosting);

        public void ApplyBoost(float magnitude, float duration) =>
            StartMultiplier(Mathf.Max(1f, magnitude), duration);

        public void ApplySlow(float factor, float duration) =>
            StartMultiplier(Mathf.Clamp01(factor), duration);

        public bool ControlsEnabled { get; private set; } = true;

        // Usato dalla UI del menu (vedi MenuControls): quando i controlli si
        // spengono il kart si freezea all'istante (velocity azzerata) e il
        // drift attivo viene resettato senza boost gratuito. La fisica
        // (gravita', sospensione, collisioni) continua a girare.
        public void SetControlsEnabled(bool value)
        {
            if (ControlsEnabled == value)
                return;

            ControlsEnabled = value;

            if (value || rb == null)
                return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            CurrentSpeed = 0f;
            lastReportedSpeed = 0f;
            OnSpeedChanged?.Invoke(0f);

            isDriftingActive = false;
            driftCharge = 0f;
            driftEntrySpeed = 0f;
            isDriftCharged = false;
            pendingCrashExit = false;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            CurrentSpeed = 0f;
            transform.SetPositionAndRotation(position, rotation);
            visualYawDegrees = rotation.eulerAngles.y;
            visualYawVelocity = 0f;
            hasVisualYawDegrees = true;
            hasVisualWorldRotation = false;

            isDriftingActive = false;
            driftCharge = 0f;
            driftEntrySpeed = 0f;
            isDriftCharged = false;
            driftLastGroundedTime = -999f;
            pendingCrashExit = false;
        }

        public void RespawnAt(Transform t)
        {
            if (t == null)
            {
                Debug.LogWarning("[KartController] RespawnAt chiamato con Transform nullo.", this);
                return;
            }

            Teleport(t.position, t.rotation);
        }

        #endregion

        #region Unity callbacks

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            // NESSUNA interpolazione: grafica (il visual) e' figlia del rigidbody e
            // viene ruotata in world space in LateUpdate. Con Interpolate attivo,
            // durante le inversioni (corpo che ruota ~49 gradi/step) il render
            // interpola la posa del padre DOPO la nostra scrittura e trascina il
            // muso di ~30 gradi in un frame: il famoso "scatto verso il mezzo".
            rb.interpolation = RigidbodyInterpolation.None;
            // Discrete, NON Continuous: il Continuous di Unity e' speculative
            // CCD e sui bordi interni dei MeshCollider del terreno (le giunzioni
            // fra i "Plane" del livello, anche complanari) genera contatti
            // fantasma con impulsi verso l'alto = micciosalto alla traversata
            // della giunzione, piu' vistoso in velocita'. A queste velocita'
            // (max ~20 u/s con step 0.02s => ~0.4 u di spread per step) il
            // tunneling e' improbabile: i muri sono box spessi e la parete
            // skate e' rilevata da OnCollisionStay, non dalla CCD.
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;

            // ===== Capsula flottante (anti ghost-bump alla radice) =====
            // Su terreno piatto i bordi di giunzione fra i collider del terreno
            // (box complanari tipo i "Base", fogli mesh) generano contatti il
            // cui impulso/depenetrazione sposta il kart verso l'alto = salto
            // visibile in velocita'. Alzando il FONDO della capsula (top
            // invariato) su terreno piatto il kart cavalca solo sulla
            // sospensione raycast e non tocca mai i bordi: niente contatto,
            // niente pop. Il contatto fisico si ristabilisce da solo dove
            // serve: su pendenze la superficie risale verso la capsula (entro
            // pochi gradi), agli atterraggi, e sui LATI (muri e pareti skate:
            // il contatto laterale non e' toccato dall'alzata del fondo).
            // NB: con la molla da sola, un damping quasi nullo lascerebbe un'
            // oscillazione visibile a riposo: sotto, tetto minimo al damping.
            // Salviamo il damping ORIGINALE (quello serializzato in scena,
            // es. 0.1) PRIMA del tetto minimo: il ramo "pendenza riperta"
            // di ApplySuspension replica la sospensione pre-fix e deve usare
            // il valore di scena, non quello elevato (sulle pareti riperte il
            // floor smorzerebbe la salita: bug gia' visto). Fuori dal blocco
            // clearance: deve valere anche se un domani la clearance torna 0.
            suspensionDampingSenzaTetto = suspensionDamping;

            if (groundContactClearance > 0f)
            {
                suspensionDamping = Mathf.Max(suspensionDamping, minSuspensionDamping);

                CapsuleCollider[] bodyCapsules = GetComponentsInChildren<CapsuleCollider>(true);
                for (int i = 0; i < bodyCapsules.Length; i++)
                {
                    CapsuleCollider capsule = bodyCapsules[i];
                    if (capsule.isTrigger)
                        continue;

                    // L'altezza della capsula vive lungo il suo asse (direction
                    // 0=X, 1=Y, 2=Z) nello spazio locale del suo transform: la
                    // conversione metro->unita' locali passa per la lossyScale
                    // sull'asse giusto (la scena usa scale non uniformi).
                    Vector3 lossyScale = capsule.transform.lossyScale;
                    float axisScale;
                    if (capsule.direction == 0) axisScale = lossyScale.x;
                    else if (capsule.direction == 1) axisScale = lossyScale.y;
                    else axisScale = lossyScale.z;

                    if (axisScale <= 0.0001f)
                        continue;

                    float clearanceLocal = groundContactClearance / axisScale;
                    float newHeight = capsule.height - clearanceLocal;

                    // Capsula valida solo con altezza >= 2*raggio: se l'alzata
                    // la degenerasse, la lasciamo com'e' (meglio il bump che
                    // una capsula rotta).
                    if (newHeight < 2f * capsule.radius)
                        continue;

                    capsule.height = newHeight;

                    // Teniamo FISSO il top: il fondo sale di clearance, quindi
                    // il centro slitta verso l'alto di meta' dell'accorciatura.
                    float centerShift = clearanceLocal * 0.5f;
                    if (capsule.direction == 0)
                        capsule.center += new Vector3(centerShift, 0f, 0f);
                    else if (capsule.direction == 1)
                        capsule.center += new Vector3(0f, centerShift, 0f);
                    else
                        capsule.center += new Vector3(0f, 0f, centerShift);
                }
            }

            input = GetComponent<IKartInput>();
            if (input == null)
                Debug.LogWarning("[KartController] Nessun IKartInput trovato su " + name + " (KartInput per il giocatore, EnemyKart per il NPC). Il kart non rispondera' a nessun input.", this);

            if (groundCheckOrigin == null)
            {
                Debug.LogWarning(
                    "[KartController] Ground Check Origin non assegnato: uso il transform principale.",
                    this
                );
                groundCheckOrigin = transform;
            }

            if (driftVisual != null)
            {
                driftVisualBaseRotation = driftVisual.localRotation;
            }

            lastValidGroundUp = Vector3.up;
            hasValidGroundUp = true;

            visualYawDegrees = transform.eulerAngles.y;
            visualYawVelocity = 0f;
            hasVisualYawDegrees = true;

            // Soffitto smorzato: parte dal valore istantaneo (cruiseSpeed a
            // spawn, boost off). Poi rampa in FixedUpdate verso EffectiveMaxSpeed.
            currentEffectiveMax = EffectiveMaxSpeed;
            // Soffitto combinato (cap * multiplier): all'avvio e' cruise*1.
            currentPlanarMax = currentEffectiveMax * speedMultiplier;
            // Nessun eccesso di drift-boost all'avvio.
            boostExcess = 0f;

            // Sottoscriviamo OnImpact: usato come trigger di "crash vero" per
            // far uscire il drift attivo solo in caso di urto forte (vedi
            // impactThreshold). I lievi sfregamenti contro muri o ostacoli NON
            // spezzano il drift: il floor planare resta sempre attivo.
            OnImpact.AddListener(HandleKartImpact);
        }

        private void OnDestroy()
        {
            OnImpact.RemoveListener(HandleKartImpact);
        }

        private void HandleKartImpact(float v)
        {
            if (isDriftingActive)
                pendingCrashExit = true;
        }

        private void FixedUpdate()
        {
            bool wasGroundedLastFrame = IsGrounded;

            UpdateGrounded();
            HandleLandingStabilization(wasGroundedLastFrame);
            UpdateSkateRampLaunchState();
            ApplySuspension();
            ApplyAirStabilization();
            UpdateCameraRelativeMoveDirection();
            UpdateActiveDrift();
            UpdateEffectiveMax();
            UpdatePlanarMax();
            UpdateSteering();
            UpdateVelocity();
        }

        // Rampa il soffitto smorzato verso EffectiveMaxSpeed. Discesa (rilascio
        // boost) a boostReleaseDeceleration, salita (pressione boost) a
        // acceleration. Cosi' il clamp planare in UpdateVelocity segue un
        // valore che scende dolcemente invece di tagliare 22->12 in un frame.
        private void UpdateEffectiveMax()
        {
            float target = EffectiveMaxSpeed;
            float rate = (target < currentEffectiveMax)
                ? boostReleaseDeceleration
                : acceleration;
            currentEffectiveMax = Mathf.MoveTowards(
                currentEffectiveMax,
                target,
                rate * Time.fixedDeltaTime
            );
        }

        // Rampa il soffitto planare COMBINATO (currentEffectiveMax + boostExcess).
        // L'eccesso e' la parte di cap sopra currentEffectiveMax dovuta al
        // speedMultiplier (drift-boost reward). Salita ISTANTANEA: quando il
        // multiplier sale (inizio drift-boost) l'eccesso scatta subito, cosi'
        // il kick non viene imbrigliato. Discesa ESPONENZIALE rapida
        // (driftBoostEndDecay): smorza il hard-cut quando il mini-turbo scade,
        // riportando il cap al normale in fretta, robusto a qualsiasi magnitude.
        // Separato da boostReleaseDeceleration (che governa solo il rilascio mouse
        // su currentEffectiveMax). Firmato: mult<1 (slow pad) = eccesso negativo.
        private void UpdatePlanarMax()
        {
            float targetExcess = currentEffectiveMax * (speedMultiplier - 1f);
            if (targetExcess >= boostExcess)
                boostExcess = targetExcess;
            else
                boostExcess = Mathf.Lerp(
                    boostExcess,
                    targetExcess,
                    1f - Mathf.Exp(-driftBoostEndDecay * Time.fixedDeltaTime)
                );
            if (Mathf.Abs(targetExcess) < 0.001f && Mathf.Abs(boostExcess) < 0.05f)
                boostExcess = 0f;
            currentPlanarMax = currentEffectiveMax + boostExcess;
        }

        private void LateUpdate()
        {
            UpdateDriftVisual();
            UpdateGroundAlignmentVisual();


        }



        private void OnCollisionEnter(Collision collision)
        {
            float v = collision.relativeVelocity.magnitude;
            if (v < impactThreshold)
                return;

            // Classificazione "muro": almeno un contatto con normale quasi
            // orizzontale (superficie verticale) e NON di tipo ground. Muri
            // veri (layer Default) matchano; le pareti-rampa skate sono su
            // layer ground e sono escluse, come gli atterraggi duri (normali
            // verso l'alto).
            bool controMuro = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                bool isGroundLayer =
                    (groundLayer.value & (1 << contact.otherCollider.gameObject.layer)) != 0;

                if (!isGroundLayer && Mathf.Abs(contact.normal.y) < sogliaNormaleMuro)
                {
                    controMuro = true;
                    break;
                }
            }

            OnImpact?.Invoke(v);
            if (controMuro)
                OnImpattoMuro?.Invoke(v);
        }

        private void OnCollisionStay(Collision collision)
        {
            float steepestAngle = maxGroundSlopeAngle;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                Vector3 n = contact.normal;
                float angle = Vector3.Angle(n, Vector3.up);

                if (angle > steepestAngle)
                {
                    // Distingue la parete verticale di una rampa da skate
                    // (collider sul layer Ground) da un muro vero (Default):
                    // sulla rampa non applichiamo il wall-avoidance; entriamo
                    // invece in modalita' "lancio" balistica, conservando la
                    // spinta residua e lasciando agire solo la gravita'.
                    bool isGroundLayer =
                        (groundLayer.value & (1 << contact.otherCollider.gameObject.layer)) != 0;

                    if (isGroundLayer)
                    {
                        lastSkateRampContactTime = Time.time;
                        // Cattura la velocity una sola volta, al momento del
                        // primo contatto con la parete verticale. Usiamo
                        // lastSetVelocity (la velocity che avevamo impostato nel
                        // FixedUpdate precedente, PRIMA che il solver delle
                        // collisioni rimuovesse la componente dentro-il-muro):
                        // cosi' conserviamo l'orientamento reale del kart subito
                        // prima di toccare la rampa e il lancio segue quella
                        // direzione (anche in avvicinamento laterale).
                        if (!skateRampLaunch)
                        {
                            skateRampLaunch = true;
                            launchVelocity = lastSetVelocity;
                            // Congela lo yaw orizzontale al momento del distacco:
                            // durante il volo parabolico il muso non segue piu'
                            // l'input del giocatore (lancio balistico), resta
                            // fisso sulla direzione in cui abbiamo lasciato il muro.
                            launchYaw = transform.eulerAngles.y;
                            visualYawDegrees = launchYaw;
                            // Cattura il PITCH dalla velocity (NON dalla posa del
                            // visual, che potrebbe essere ancora orizzontale per
                            // via dello slerp lagging). La velocity rappresenta
                            // l'orientamento reale del kart subito prima di
                            // toccare il muro: se stava salendo la rampa slopeata
                            // a 80 gradi, frozenPitch sara' ~80 gradi (muso in su).
                            // Resta FISSO per tutto il volo fino all'atterraggio.
                            Vector3 pf = new Vector3(launchVelocity.x, 0f, launchVelocity.z);
                            float pm = pf.magnitude;
                            frozenPitch = (pm > 0.001f)
                                ? Mathf.Atan2(launchVelocity.y, pm) * Mathf.Rad2Deg
                                : (launchVelocity.y > 0f ? 90f : 0f);
                            // Cattura la NORMALE del muro al primo contatto: sara'
                            // l'"up" del kart per tutto il volo, cosi' le 4 ruote
                            // restano "attaccate" alla parete verticale. La
                            // normale punta verso l'esterno del muro (e' il
                            // reference up come se il kart guidasse sulla parete).
                            launchWallNormal = n;
                            // NON azzeriamo hasVisualWorldRotation: la
                            // RotateTowards nel branch di lancio parte dalla
                            // posa attuale del visual (allineata al terreno
                            // slope) e transita gradualmente verso la posa di
                            // lancio al rate di skateRampVisualTurnSpeed
                            // gradi/sec. Se lo azzerassimo, il primo frame
                            // snap-erebbe subito al target = scatto visibile.
                        }
                        continue;
                    }

                    steepestAngle = angle;
                    steepWallNormal = n;
                    steepWallPoint = contact.point;
                    lastWallContactTime = Time.time;
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (groundCheckOrigin != null)
            {
                Gizmos.color = (Application.isPlaying && IsGrounded) ? Color.green : Color.red;
                Vector3 from = groundCheckOrigin.position;
                Vector3 to = from + Vector3.down * groundCheckDistance;
                Gizmos.DrawLine(from, to);
                Gizmos.DrawWireSphere(from, groundCheckRadius);
                Gizmos.DrawWireSphere(to, groundCheckRadius);

                Gizmos.color = Color.cyan;
                Vector3 ridePoint = from + Vector3.down * rideHeight;
                Gizmos.DrawWireSphere(ridePoint, 0.08f);
            }

            DrawProbeGizmo(frontLeftGroundProbe);
            DrawProbeGizmo(frontRightGroundProbe);
            DrawProbeGizmo(rearLeftGroundProbe);
            DrawProbeGizmo(rearRightGroundProbe);


            if (Application.isPlaying && WallContactActive)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(steepWallPoint, steepWallPoint + steepWallNormal);
                Gizmos.DrawWireSphere(steepWallPoint, 0.06f);
            }
        }

        #endregion

        #region Internal

        private Rigidbody rb;
        private IKartInput input;

        // Gate dei controlli: KartInput cacha i valori letti in Update, quindi
        // disattivarlo congelerebbe l'ultimo input (W tenuto = accelerazione
        // eterna). Passiamo invece tutte le letture da questi helper, che
        // restituiscono valori neutri quando ControlsEnabled e' false.
        private Vector2 CurrentMove =>
            (ControlsEnabled && input != null) ? input.Move : Vector2.zero;

        private bool CurrentBrake =>
            ControlsEnabled && input != null && input.Brake;

        // Boost (mouse sx): alza il soffitto di velocita' da cruiseSpeed a
        // maxSpeed. Gated da ControlsEnabled come gli altri input, cosi' a
        // menu/disattivato non resta appeso.
        private bool CurrentBoost =>
            ControlsEnabled && input != null && input.Boost;

        // True mentre il boost (mouse sx per il giocatore) e' tenuto: il
        // soffitto di velocita' passa da cruiseSpeed a maxSpeed. Esposto per
        // la fase corsa dell'animazione del personaggio (ArtiPersonaggio).
        public bool IsBoosting => CurrentBoost;

        // Soffitto di velocita' effettivo in base al boost. Usato ovunque si
        // calcolava target/planarMax con maxSpeed (UpdateVelocity/UpdateSteering).
        private float EffectiveMaxSpeed =>
            CurrentBoost ? maxSpeed : cruiseSpeed;

        // Soffitto di velocita' "smorzato": rampa dolcemente verso
        // EffectiveMaxSpeed invece di saltare. Evita il taglio istantaneo del
        // clamp planare quando rilasci il boost (mouse sx). Discesa a
        // boostReleaseDeceleration, salita a acceleration. E' il valore
        // effettivamente usato come cap/target in UpdateVelocity/UpdateSteering.
        private float currentEffectiveMax;

        // Soffitto planare COMBINATO smorzato = currentEffectiveMax * speedMultiplier.
        // Smorza anche il hard-cut del multiplier del drift-boost reward (1.5 -> 1
        // alla fine del mini-turbo), che currentEffectiveMax da solo non copre.
        // Salita istantanea (non annacqua il kick del drift boost), discesa a
        // boostReleaseDeceleration. Usato come cap/target effettivo in UpdateVelocity.
        private float currentPlanarMax;

        // Eccesso di cap sopra currentEffectiveMax, derivante dal speedMultiplier
        // (drift-boost reward). Smorzato con decadimento ESPONENZIALE rapido
        // (driftBoostEndDecay), separato dal boostReleaseDeceleration (che governa
        // solo il rilascio mouse). Cosi' il cap torna normale in fretta dopo il
        // drift-boost, anche con activeDriftBoostMagnitude alto. Firmato: gestisce
        // anche mult<1 (slow pad) come eccesso negativo.
        private float boostExcess;

        // Peso della fase corsa (0 = camminata, 1 = corsa) per la fusione dei
        // set Grip/Drift. Derivato dal soffitto smorzato currentEffectiveMax:
        // segue le stesse rampe del boost (salita ad 'acceleration' alla
        // pressione, discesa a 'boostReleaseDeceleration' al rilascio), quindi
        // il grip resta "da corsa" finche' il soffitto e' alto e transita
        // dolcemente su quello "da camminata" mentre la velocita' cala. Nessuno
        // stato da tenere in sync. In modalita' AI e' fissato a 1 (corsa): il
        // NPC non ha il boost (EnemyKart.Boost => false) e col peso derivato
        // cadrebbe per sempre sul set camminata perdendo la sintonia storica.
        private float PesoCorsa =>
            aiSteeringMode
                ? 1f
                : Mathf.Clamp01(Mathf.InverseLerp(cruiseSpeed, maxSpeed, currentEffectiveMax));

        // Valori Grip/Drift fusi per fase (camminata <-> corsa via PesoCorsa).
        // Usati da IsDrifting, UpdateSteering, UpdateVelocity e
        // UpdateDriftVisual al posto dei campi grezzi.
        private float groundLateralFrictionCorrente =>
            Mathf.Lerp(groundLateralFrictionCamminata, groundLateralFriction, PesoCorsa);

        private float airLateralFrictionCorrente =>
            Mathf.Lerp(airLateralFrictionCamminata, airLateralFriction, PesoCorsa);

        private float driftLateralFrictionCorrente =>
            Mathf.Lerp(driftLateralFrictionCamminata, driftLateralFriction, PesoCorsa);

        private float driftMinSpeedCorrente =>
            Mathf.Lerp(driftMinSpeedCamminata, driftMinSpeed, PesoCorsa);

        private float driftMinSteerCorrente =>
            Mathf.Lerp(driftMinSteerCamminata, driftMinSteer, PesoCorsa);

        private float driftSteerBoostCorrente =>
            Mathf.Lerp(driftSteerBoostCamminata, driftSteerBoost, PesoCorsa);

        private float driftVisualYawDegreesCorrente =>
            Mathf.Lerp(driftVisualYawDegreesCamminata, driftVisualYawDegrees, PesoCorsa);

        private float driftVisualLerpSpeedCorrente =>
            Mathf.Lerp(driftVisualLerpSpeedCamminata, driftVisualLerpSpeed, PesoCorsa);

        // Valori Steering fusi per fase (stesso meccanismo del Grip/Drift).
        // Usati da UpdateSteering (turnRate, turnAtRest,
        // cameraRelativeTurnResponsiveness, shoppingCartSteerLoss) e da
        // UpdateVelocity (shoppingCartSlip, shoppingCartSlipSteerThreshold).
        private float turnRateCorrente =>
            Mathf.Lerp(turnRateCamminata, turnRate, PesoCorsa);

        private float turnAtRestCorrente =>
            Mathf.Lerp(turnAtRestCamminata, turnAtRest, PesoCorsa);

        private float shoppingCartSteerLossCorrente =>
            Mathf.Lerp(shoppingCartSteerLossCamminata, shoppingCartSteerLoss, PesoCorsa);

        private float shoppingCartSlipCorrente =>
            Mathf.Lerp(shoppingCartSlipCamminata, shoppingCartSlip, PesoCorsa);

        private float shoppingCartSlipSteerThresholdCorrente =>
            Mathf.Lerp(shoppingCartSlipSteerThresholdCamminata, shoppingCartSlipSteerThreshold, PesoCorsa);

        private float cameraRelativeTurnResponsivenessCorrente =>
            Mathf.Lerp(cameraRelativeTurnResponsivenessCamminata, cameraRelativeTurnResponsiveness, PesoCorsa);

        private bool CurrentDrift =>
            ControlsEnabled && input != null && input.Drift;
        private Coroutine multiplierRoutine;
        private float speedMultiplier = 1f;
        private bool wasGrounded;
        private float lastReportedSpeed;
        private Quaternion driftVisualBaseRotation = Quaternion.identity;
        private float currentDriftYaw;

        private bool isDriftingActive;
        private float driftCharge;
        private float driftEntrySpeed;
        private bool isDriftCharged;
        private float driftLastGroundedTime = -999f;
        private bool pendingCrashExit;
        private float visualYawDegrees;
        private float visualYawVelocity;
        private bool hasVisualYawDegrees;
        private Quaternion visualWorldRotation = Quaternion.identity;
        private bool hasVisualWorldRotation;
        private RaycastHit groundHit;
        private float lastGroundedTime;
        private bool hasGroundContactThisFrame;

        // ===== Filtro anti ghost-bump (giunzioni del terreno) =====
        // Con la capsula flottante (groundContactClearance > 0) su terreno
        // piatto non arriva piu' nessun contatto fisico col terreno, quindi il
        // clamp in UpdateVelocity e' una rete di sicurezza per gli impulsi +Y
        // residui (piccoli scalini, giunzioni su dolci pendenze).

        // Damping sospensione ORIGINALE (serializzato in scena, es. 0.1),
        // salvato in Awake PRIMA del tetto minimo minSuspensionDamping. Usato
        // SOLO dal ramo pre-fix di ApplySuspension per le pendenze riperte:
        // li' il damping elevato smorzerebbe la salita (velocita' terminale
        // di caduta gravity/damping e molla annullata oltre vy ~ spring/damp).
        private float suspensionDampingSenzaTetto;

        private float lastWallContactTime = -999f;
        private Vector3 steepWallNormal;
        private Vector3 steepWallPoint;
        private bool skateRampLaunch;
        private float lastSkateRampContactTime = -999f;
        private Vector3 launchVelocity;
        private Vector3 lastSetVelocity;
        private float launchYaw;
        private float frozenPitch;
        private Vector3 launchWallNormal = Vector3.up;
        private Vector3 lastValidGroundUp = Vector3.up;
        private bool hasValidGroundUp;
        private Vector3 desiredMoveDirection;
        private float desiredMoveAmount;
        private float lastSteerAmount;
        private float currentSignedAngleToDesired;
        private bool isReorientingFromStop;
        private bool isReorientingWhileMoving;

        private bool WallContactActive => (Time.time - lastWallContactTime) <= wallContactGraceTime;

        private void UpdateGrounded()
        {
            bool hitGround = TrySphereCastGround(
                groundCheckOrigin.position,
                groundCheckRadius,
                Vector3.down,
                groundCheckDistance,
                out groundHit
            );

            hasGroundContactThisFrame = hitGround;

            if (hitGround)
                lastGroundedTime = Time.time;

            bool grounded = hitGround || (Time.time - lastGroundedTime) <= groundedGraceTime;
            IsGrounded = grounded;

            if (grounded != wasGrounded)
            {
                wasGrounded = grounded;
                OnGroundedChanged?.Invoke(grounded);
            }
        }

        private void HandleLandingStabilization(bool wasGroundedLastFrame)
        {
            if (!wasGroundedLastFrame && IsGrounded)
            {
                Vector3 av = rb.angularVelocity;
                av.y *= landingAngularDampingFactor;
                rb.angularVelocity = av;
            }
        }

private void UpdateSkateRampLaunchState()
        {
            if (!skateRampLaunch)
                return;

            // Finche' tocchiamo la parete verticale restiamo in lancio, anche se
            // lo SphereCast verso il basso becca ancora la parte slopeata della
            // rampa sotto di noi (e' "camminabile", quindi farebbe scattare
            // IsGrounded, azzerando il lancio e riattivando sospensione/wall
            // avoidance che respingono il kart sul muro). Solo quando ci siamo
            // staccati dalla parete verticale accettiamo di nuovo il grounding.
            bool stillTouchingWall =
                (Time.time - lastSkateRampContactTime) <= wallContactGraceTime;

            if (stillTouchingWall)
                return;

            // Siamo staccati dalla parete: restiamo in lancio per tutta la
            // fase aerea (volo parabolico). Usciamo SOLO quando riatterriamo
            // su una superficie camminabile. Questo disabilita sterzo e
            // air-control per tutto il volo, come richiesto.
            if (IsGrounded && hasGroundContactThisFrame)
            {
                skateRampLaunch = false;
            }
        }

        private void ApplyAirStabilization()
        {
            // Durante il lancio skate azzeriamo la angular velocity: vogliamo un
            // volo balistico pulito, senza rotazioni residue del corpo ne'
            // intervento dell'air-control (i FreezeRotationX/Z sono attivi, ma
            // azzeriamo anche il yaw per sicurezza e pulizia visiva).
            if (skateRampLaunch)
            {
                rb.angularVelocity = Vector3.zero;
                return;
            }

            if (IsGrounded)
            {
                rb.angularVelocity = Vector3.zero;
                return;
            }

            Vector3 av = rb.angularVelocity;
            av.x = 0f;
            av.z = 0f;
            av.y = Mathf.Clamp(av.y, -maxAirYawAngularVelocity, maxAirYawAngularVelocity);
            av.y = Mathf.MoveTowards(av.y, 0f, airAngularDamping * Time.fixedDeltaTime);
            rb.angularVelocity = av;
        }

        private void ApplySuspension()
        {
            // Durante il lancio skate disattiviamo la sospensione: lo SphereCast
            // centrale verso il basso becca ancora la parte slopeata della rampa
            // sotto la parete verticale e la molla aggiungerebbe una spinta
            // extra non dovuta (il kart schizzerebbe sul muro anche a bassa
            // velocita'). In lancio vogliamo solo inerzia + gravita'.
            if (skateRampLaunch)
                return;

            if (!hasGroundContactThisFrame)
                return;

            Vector3 groundNormal = groundHit.normal;
            float dampScale = Mathf.Clamp01((groundNormal.y - 0.34f) / 0.36f);

            // ===== Pendenze riperte (oltre ~70 gradi): sospensione PRE-FIX =====
            // Replichiamo ESATTAMENTE la sospensione del commit 6bbcb1c
            // (compression sulla distanza verticale clampata, mai forza verso
            // il basso, damping debole originale sulla vy assoluta, early
            // return se la forza non e' positiva): e' la dinamica con cui le
            // rampe/pareti skate si sono sempre comportate. Le varianti
            // slope-aware (perpendicolare/deviazione/pull-down) cambiano il
            // feel dell'arrivo alla parete e non sono volute qui.
            if (dampScale <= 0f)
            {
                float compressionPreFix =
                    Mathf.Clamp(rideHeight - groundHit.distance, 0f, rideHeight);
                if (compressionPreFix <= 0f)
                    return;

                float totalPreFix =
                    compressionPreFix * suspensionStrength
                    - rb.linearVelocity.y * suspensionDampingSenzaTetto;
                if (totalPreFix <= 0f)
                    return;

                rb.AddForce(Vector3.up * totalPreFix, ForceMode.Acceleration);
                return;
            }

            // ===== Piatto e pendenze dolci: sospensione slope-aware =====
            // Molla BIDIREZIONALE sulla distanza PERPENDICOLARE al piano:
            // compression > 0 = schiacciata (spinge in su), < 0 = estesa (tira
            // VERSO il terreno). Il cast e' verticale: su una pendenza la
            // distanza verticale e' gonfiata di 1/cos(theta) e usarla cosi'
            // com'e' rende la molla falsamente "estesa" man mano che la
            // pendenza cresce; moltiplicare per normal.y riporta la quota
            // perpendicolare reale. Con la capsula flottante il contatto a
            // riposo su piatto non c'e': senza il tiro verso il basso, in
            // discesa il kart planerebbe invece di inseguirlo.
            float perpendicularDistance = groundHit.distance * groundNormal.y;
            float compression = rideHeight - perpendicularDistance;
            float springForce = compression * suspensionStrength;

            // Smorzamento sulla DEVIAZIONE dalla vy che seguirebbe la pendenza
            // (NON sulla vy assoluta): col damping sulla vy assoluta la caduta
            // aveva una velocita' terminale gravity/damping (in discesa il kart
            // "vola" perche' non scende abbastanza) e la molla veniva annullata
            // appena vy superava spring/damping (rampa imprendibile).
            // expectedVy e' la vy necessaria per scorrere sulla superficie
            // sotto il kart: negativa in discesa, positiva in salita, 0 su
            // piatto. (dampScale > 0 e' garantito qui: le pendenze riperte
            // sono uscite sopra col ramo pre-fix.)
            float safeNy = Mathf.Max(0.05f, groundNormal.y);
            float expectedVy =
                -(rb.linearVelocity.x * groundNormal.x +
                  rb.linearVelocity.z * groundNormal.z) / safeNy;
            float deviationVy = rb.linearVelocity.y - expectedVy;
            float dampingForce = Mathf.Clamp(
                -deviationVy * suspensionDamping * dampScale,
                -gravity,
                gravity
            );

            float totalForce = springForce + dampingForce;

            // Tetto al trascinamento verso il basso (questo ramo gira solo su
            // piatto/pendenze dolci, dove inseguire il terreno e' voluto: le
            // pendenze riperte sono uscite sopra col ramo pre-fix push-only).
            totalForce = Mathf.Max(totalForce, -suspensionMaxPullDown);

            rb.AddForce(Vector3.up * totalForce, ForceMode.Acceleration);
        }

        private void UpdateCameraRelativeMoveDirection()
        {
            Vector2 moveInput = CurrentMove;
            desiredMoveAmount = Mathf.Clamp01(moveInput.magnitude);

            if (desiredMoveAmount <= 0.001f)
            {
                desiredMoveDirection = Vector3.zero;
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Vector3 fallback = new Vector3(moveInput.x, 0f, moveInput.y);
                desiredMoveDirection = fallback.normalized;
                return;
            }

            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            Vector3 move =
                camForward * moveInput.y +
                camRight * moveInput.x;

            if (move.sqrMagnitude <= 0.001f)
            {
                desiredMoveDirection = Vector3.zero;
                return;
            }

            desiredMoveDirection = move.normalized;
        }

        private void UpdateActiveDrift()
        {
            // Gate oggetto: se l'oggetto di controllo non e' attivo, il drift
            // (attivo e passivo) e' disabilitato. Qui impedisce l'ingresso nel
            // drift attivo e, se era in corso, esce subito senza boost (reset
            // pulito). Il drift passivo e' gated in IsDrifting.
            if (!ActiveDriftEnabled)
            {
                if (isDriftingActive)
                {
                    isDriftingActive = false;
                    driftCharge = 0f;
                    driftEntrySpeed = 0f;
                    isDriftCharged = false;
                }
                return;
            }

            // FLOOR PLANARE applicato in testa (prima dell'exit-check): tira su
            // planarSpeed a >= floor SEMPRE (non piu' gated su WallContactActive).
            // Cosi' nemmeno un lieve sfregamento contro un muro/ostacolo fa
            // collassare planarSpeed sotto activeDriftMinSpeed causando false
            // exit. Il crash vero e' gestito da OnImpact (vedi pendingCrashExit),
            // non dalla velocita'.
            if (isDriftingActive && !CurrentBrake)
            {
                Vector3 pf = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                float pm = pf.magnitude;
                if (pm > 0.0001f && pm < activeDriftMinForwardSpeed)
                {
                    float scale = activeDriftMinForwardSpeed / pm;
                    Vector3 fv = rb.linearVelocity;
                    fv.x *= scale;
                    fv.z *= scale;
                    rb.linearVelocity = fv;
                }
            }

            Vector3 planarVel = rb.linearVelocity;
            planarVel.y = 0f;
            float planarSpeed = planarVel.magnitude;

            // Calcoli condivisi (inclusi per log), calcolati qui fuori dal branch.
            Vector3 bodyFwdXZ = Vector3.Scale(transform.forward, new Vector3(1f, 0f, 1f));
            if (bodyFwdXZ.sqrMagnitude > 0.0001f) bodyFwdXZ.Normalize();
            else bodyFwdXZ = Vector3.forward;
            float fwdDot = Vector3.Dot(bodyFwdXZ, planarVel);
            bool goingForward = fwdDot >= 0f;
            float angleToJoystick = Mathf.Abs(currentSignedAngleToDesired);
            float moveX = CurrentMove.x;
            bool driftHeld = CurrentDrift;
            bool boostHeld = CurrentBoost;
            bool wallContact = WallContactActive;

            if (isDriftingActive)
            {
                // Aggiorna timer di grounding per la grace. Bump brevi su curb
                // (IsGrounded=false per pochi frame) NON spezzano il drift:
                // servono activeDriftExitGraceTime di volo prolungato per
                // uscire (salto vero, skate ramp).
                if (IsGrounded)
                    driftLastGroundedTime = Time.time;

                bool lostGround = (Time.time - driftLastGroundedTime) > activeDriftExitGraceTime;
                bool releasedDrift = !driftHeld;
                // Rilascio del mouse (boost) fa uscire subito il drift attivo,
                // come il rilascio di Shift: il drift attivo e' "boost-gated".
                bool releasedBoost = !boostHeld;
                // Crash vero: gestito da OnImpact (urto >= impactThreshold).
                // pendingCrashExit e' settato in HandleKartImpact e consumato qui.
                // I lievi sfregamenti (sotto threshold) NON causano uscita: il
                // floor planare protegge la velocita' sempre.
                bool hardCrash = pendingCrashExit;
                pendingCrashExit = false;

                if (lostGround || releasedDrift || releasedBoost || hardCrash)
                {
                    // Uscita INTELLIGENTE:
                    //  - Rilascio Shift o del mouse (boost) con carica completata
                    //    -> ApplyBoost (singola fase, magnitude/duration fissi).
                    //  - Crash (OnImpact forte) o salto prolungato ->
                    //    reset senza boost. isDriftCharged decide se boostare,
                    //    NON driftCharge direttamente, cosi' il boost si ha
                    //    solo se hai sterzato abbastanza a lungo.
                    if ((releasedDrift || releasedBoost) && isDriftCharged)
                    {
                        ApplyBoost(activeDriftBoostMagnitude, activeDriftBoostDuration);

                        // KICK istantaneo: oltre al multiplier (graduale via
                        // speedMultiplier), applichiamo uno scatto immediato
                        // di velocity lungo il forward del muso. Cosi' il boost
                        // si vede appena rilasciato, invece di dover aspettare
                        // che l'acceleration porti la speed verso il nuovo
                        // target. Necessario perche' dopo un drift attivo la
                        // forwardSpeed locale parte bassa (floor) e il solo
                        // multiplier impiega ~0.8s per rendersi visibile.
                        // Skip se Brake (frena comunque).
                        if (activeDriftBoostKick > 0f && !CurrentBrake && IsGrounded)
                        {
                            Vector3 fwd = Vector3.Scale(transform.forward, new Vector3(1f, 0f, 1f)).normalized;
                            if (fwd.sqrMagnitude > 0.001f)
                            {
                                Vector3 v = rb.linearVelocity;
                                v.x += fwd.x * activeDriftBoostKick;
                                v.z += fwd.z * activeDriftBoostKick;
                                rb.linearVelocity = v;
                            }
                        }
                    }

                    isDriftingActive = false;
                    driftCharge = 0f;
                    driftEntrySpeed = 0f;
                    isDriftCharged = false;
                    return;
                }

                // Carica boost: accumula driftCharge SOLO se stai curvando davvero,
                // cioe' c'e' un angolo effettivo fra muso del kart e direzione
                // del joystick (|angolo| >= activeDriftChargeMinAngle). Non
                // basta premere W+D con muso gia' allineato (angolo=0): in quel
                // caso NON carichi. Per caricare devi essere in curva, con il
                // muso del kart ancora non allineato alla direzione di sterzo.
                // STICKY: se smetti di curvare (muso si allinea, angolo -> 0)
                // ma tieni Shift, la carica accumulata NON decade: rimane ferma
                // al valore raggiunto. Riprende a salire se ricominci a
                // curvare (angolo risale sopra soglia). Cosi' il giocatore
                // puo' caricare a tratti, rilasciando lo sterzo fra una
                // curva e l'altra, senza perdere il progresso.
                bool charging = angleToJoystick >= activeDriftChargeMinAngle;
                if (charging)
                {
                    driftCharge = Mathf.Min(
                        driftCharge + driftChargeRate * Time.fixedDeltaTime,
                        activeDriftChargeTime
                    );
                }

                // Sticky charged: una volta raggiunta la soglia resta true
                // fino al reset (rilascio Shift / crash). Niente decay.
                if (!isDriftCharged && driftCharge >= activeDriftChargeTime)
                {
                    isDriftCharged = true;
                }
            }
            else
            {
                // Anti-retromarcia: ri-entra in drift attivo SOLO se la velocity
                // ha componente >= 0 lungo il forward del muso. Se il muso e'
                // oltre 90 gradi rispetto alla velocity (es. dopo un'inversione
                // che ha lasciato il kart momentaneamente "all'indietro"), la
                // re-entry e' negata finche' la planarSpeed non ripassa nello
                // stesso emisfero del muso. Previene il "drift attivo all'indietro"
                // visto dopo le uscite brevi.
                // Inoltre richiede il boost (mouse sx) oltre a Shift: il drift
                // attivo e' "boost-gated", si attiva solo mentre boosti.
                bool canEnter =
                    driftHeld
                    && boostHeld
                    && IsGrounded
                    && planarSpeed >= activeDriftMinSpeed
                    && Mathf.Abs(moveX) >= activeDriftMinSteer
                    && goingForward;

                if (canEnter)
                {
                    // Fix 4: driftEntrySpeed basato sulla componente forward
                    // locale (localVelocity.z) invece di planarSpeed spurio.
                    // Cosi' se entri in drift da una derapata laterale, il
                    // target di retention non eccede la componente long. reale.
                    Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
                    float fwdSpeed = Mathf.Max(0f, localVel.z);
                    if (fwdSpeed < 0.5f) fwdSpeed = planarSpeed;

                    isDriftingActive = true;
                    driftEntrySpeed = fwdSpeed;
                    driftCharge = 0f;
                    isDriftCharged = false;
                    driftLastGroundedTime = Time.time;
                    pendingCrashExit = false;
                }
            }
        }

        private void UpdateSteering()
        {
            currentSignedAngleToDesired = 0f;
            lastSteerAmount = 0f;
            isReorientingFromStop = false;
            isReorientingWhileMoving = false;

            // Lancio skate: balistico, nessuno sterzo da input.
            if (skateRampLaunch)
                return;

            // ===== DRIFT ATTIVO: sterzata graduale che segue il joystick =====
            // Il muso rincorre desiredMoveDirection (joystick, camera-relative)
            // come farebbe la sterzata normale, ma con un cap hard dedicato
            // (activeDriftMaxTurnRate gradi/sec) che previene spin istantanei.
            // Niente offset oversteer, niente target "oltre la curva": la
            // derapata nasce solo dalla fisica (grip laterale abbassato +
            // velocity longitudinale mantenuta), non dal muso forzato oltre
            // la sterzo. Cosi' il kart segue il joystick in curve complete
            // (anche tornanti / 360) senza bloccarsi a 180 gradi.
            // Mathf.DeltaAngle gestisce il wrap-around a 180 senza flip.
            if (isDriftingActive)
            {
                if (desiredMoveDirection.sqrMagnitude <= 0.001f)
                    return;

                Vector3 driftCurrentFwd = transform.forward;
                driftCurrentFwd.y = 0f;
                driftCurrentFwd.Normalize();

                Vector3 driftDesiredFwd = desiredMoveDirection;
                driftDesiredFwd.y = 0f;
                driftDesiredFwd.Normalize();
                if (driftDesiredFwd.sqrMagnitude < 0.001f)
                    driftDesiredFwd = driftCurrentFwd;

                float targetYaw = Mathf.Atan2(driftDesiredFwd.x, driftDesiredFwd.z) * Mathf.Rad2Deg;
                float currentYaw = Mathf.Atan2(driftCurrentFwd.x, driftCurrentFwd.z) * Mathf.Rad2Deg;
                float driftDeltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);

                float driftMaxStep = activeDriftMaxTurnRate * Time.fixedDeltaTime;
                float driftAppliedYaw = Mathf.Clamp(driftDeltaYaw, -driftMaxStep, driftMaxStep);
                transform.Rotate(0f, driftAppliedYaw, 0f, Space.World);

                // Angolo fra muso e joystick: usato da Step 3 per la carica
                // boost (carica solo se |angolo| >= activeDriftChargeMinAngle).
                currentSignedAngleToDesired = driftDeltaYaw;
                lastSteerAmount = 1f;
                return;
            }

            if (desiredMoveDirection.sqrMagnitude <= 0.001f)
                return;

            Vector3 currentForward = transform.forward;
            currentForward.y = 0f;
            currentForward.Normalize();

            Vector3 desiredForward = desiredMoveDirection;
            desiredForward.y = 0f;
            desiredForward.Normalize();

            float signedAngle = Vector3.SignedAngle(currentForward, desiredForward, Vector3.up);
            float absAngle = Mathf.Abs(signedAngle);
            currentSignedAngleToDesired = signedAngle;

            // SNAP ISTANTANEO per grandi cambi di direzione (inversioni, tornanti
            // stretti, rotazioni ad O da fermo): ruoto subito il corpo di tutta
            // l'angolatura residua e reindirizzo la componente longitudinale della
            // velocity lungo il nuovo forward, preservando l'energia. La parte
            // laterale preesistente viene lasciata al normale smorzamento della
            // grip. Sotto la soglia si ricade nel comportamento graduale originale
            // (carrello della spesa con slip e drift).
            // In modalita' AI bypassiamo lo snap: il NPC non deve scattare verso
            // la nuova direzione su grandi cambi di angolo, ma sterzare sempre
            // gradualmente (vedi richiesta: "constantemente sterzare").
            if (!aiSteeringMode && absAngle >= instantRealignAngle)
            {
                Vector3 vel = rb.linearVelocity;
                Vector3 planarVel = new Vector3(vel.x, 0f, vel.z);
                float fwdComp = Vector3.Dot(planarVel, currentForward);
                Vector3 lateralRemainder = planarVel - currentForward * fwdComp;

                transform.Rotate(0f, signedAngle, 0f, Space.World);

                Vector3 newForwardXZ = transform.forward;
                newForwardXZ.y = 0f;
                newForwardXZ.Normalize();

                float keptFwd = fwdComp * instantRealignLongitudinalRetention;
                float residualFwd = fwdComp * (1f - instantRealignLongitudinalRetention);
                Vector3 newPlanarVel =
                    newForwardXZ * keptFwd
                    + lateralRemainder
                    + currentForward * residualFwd;

                rb.linearVelocity = new Vector3(newPlanarVel.x, vel.y, newPlanarVel.z);

                currentSignedAngleToDesired = 0f;
                lastSteerAmount = 0f;
                return;
            }

            // GRADUALE: comportamento originale per angoli piccoli. Il corpo
            // ruota a turn-rate, mentre la velocity mondiale continua dritta:
            // si apre un angolo fra forward e velocity e nasce lo slittamento
            // laterale assorbito progressivamente dalla grip (carrello della
            // spesa). shoppingCartSlip riduce la grip alle alte velocita' in
            // curva e isDrifting la abbassa ulteriormente col tasto drift.
            // In modalita' AI saltiamo del tutto il rilevamento di
            // reorientation (ruota-prima-di-muoversi da fermo e inversione in
            // corsa): il NPC non deve mai limitare l'accelerazione ne'
            // applicare la frenata di reorientation, deve solo sterzare.
            if (!aiSteeringMode)
            {
                Vector3 planarVelocity = rb.linearVelocity;
                planarVelocity.y = 0f;
                float planarSpeed = planarVelocity.magnitude;

                bool nearStopped = planarSpeed <= rotateBeforeMoveSpeedThreshold;
                bool movingFastEnough = planarSpeed >= movingReorientationMinSpeed;

                if (nearStopped && absAngle > rotateBeforeMoveReleaseAngle)
                {
                    isReorientingFromStop = true;
                }
                else if (
                    IsGrounded &&
                    movingFastEnough &&
                    absAngle >= movingReorientationEnterAngle &&
                    desiredMoveAmount > 0.001f
                )
                {
                    isReorientingWhileMoving = true;
                }
                else if (
                    IsGrounded &&
                    movingFastEnough &&
                    absAngle > movingReorientationExitAngle &&
                    Vector3.Dot(currentForward, desiredForward) < 0f
                )
                {
                    isReorientingWhileMoving = true;
                }
            }

            float normalizedTurnInput = Mathf.Clamp(signedAngle / 90f, -1f, 1f);

            float speedRatio = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, currentEffectiveMax));
            // In modalita' AI usiamo sempre turnFactor 1: niente riduzione
            // turnAtRest a bassa velocita', cosi' il NPC sterza a rate pieno
            // verso la direzione desiderata anche da fermo o in manovra.
            float turnFactor = aiSteeringMode ? 1f : Mathf.Lerp(turnAtRestCorrente, 1f, speedRatio);
            float effectiveTurn = turnRateCorrente * turnFactor;

            if (isReorientingFromStop || isReorientingWhileMoving)
            {
                effectiveTurn = Mathf.Max(effectiveTurn, turnRateCorrente * cameraRelativeTurnResponsivenessCorrente);
            }

            // In aria il kart perde grip di sterzata (airControl); in modalita'
            // AI bypassiamo perche' il NPC deve poter sterzare SEMPRE, anche in
            // volo: il suo airControl e' 0 (copiato dal giocatore) e senza
            // questo bypass resterebbe bloccato dritto in una direzione fissa
            // ("sempre verso su") appena perde il contatto col terreno.
            if (!aiSteeringMode && !IsGrounded)
                effectiveTurn *= airControl;

            if (CurrentDrift && IsGrounded)
                effectiveTurn *= driftSteerBoostCorrente;

            float steerLossMultiplier = Mathf.Lerp(1f, 1f - shoppingCartSteerLossCorrente, speedRatio);
            effectiveTurn *= steerLossMultiplier;

            float maxStep = effectiveTurn * Time.fixedDeltaTime;
            float appliedYaw = Mathf.Clamp(signedAngle, -maxStep, maxStep);

            transform.Rotate(0f, appliedYaw, 0f, Space.World);
            lastSteerAmount = Mathf.Abs(normalizedTurnInput);
        }

        private void UpdateVelocity()
        {
            if (skateRampLaunch)
            {
                // Lancio skate: balistico puro. Conserviamo la spinta residua
                // catturata al momento del primo contatto (launchVelocity) e
                // applichiamo solo la gravita'. Nessuna accelerazione da input,
                // nessun wall-avoidance, nessuna sospensione: il kart "scala"
                // la parete verticale della rampa (layer Ground) seguendo
                // l'orientamento che aveva subito prima di toccarla e ricade
                // come uno skate.
                launchVelocity.y -= gravity * Time.fixedDeltaTime;
                rb.linearVelocity = launchVelocity;

                Vector3 localVel = transform.InverseTransformDirection(launchVelocity);
                CurrentSpeed = localVel.z;

                if (Mathf.Abs(CurrentSpeed - lastReportedSpeed) > 0.05f)
                {
                    lastReportedSpeed = CurrentSpeed;
                    OnSpeedChanged?.Invoke(CurrentSpeed);
                }
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
            float lateralSpeed = localVelocity.x;
            float forwardSpeed = localVelocity.z;
            float verticalSpeed = rb.linearVelocity.y;

            // Rete di sicurezza anti ghost-bump (giunzioni del terreno): se un
            // contatto fisico applica un impulso +Y mentre siamo grounded su
            // terreno ~piatto, lo limitiamo alla salita legittima attesa dalla
            // pendenza dell'hit (speed planare * tan(angolo)) piu' una piccola
            // tolleranza. Con la capsula flottante (groundContactClearance > 0)
            // su terreno piatto non arriva piu' alcun contatto, quindi il
            // filtro resta quasi sempre silente: copre i casi residui (scalini
            // piccoli, giunzioni su dolci pendenze). Il gate usa la normale
            // dell'hit PIU' VICINO: su pendenze riperte (rampe) e' inclinata e
            // spegne il filtro, cosi' la salita legittima resta libera; la
            // discesa (-Y), l'aria e il lancio skate non sono toccati.
            // Requisito AGGIUNTIVO: la molla deve essere compressa (kart
            // realmente APPOGGIATO, quota perpendicolare sotto rideHeight).
            // Senza questo, l'arco di lancio skate oltre il bordo della parete
            // vedrebbe il piatto della piattaforma sotto di lui (cast a d
            // piccola) e verrebbe cap-pato mentre sale: vy > 0 + hit piatto
            // NON bastano a dire "sto facendo un ghost bump", serve il
            // supporto. I ghost bump reali avvengono a kart appoggiato
            // (log: d 0.11-0.13 -> compression ~0.28 > 0).
            float supportCompression =
                rideHeight - groundHit.distance * groundHit.normal.y;
            if (IsGrounded
                && hasGroundContactThisFrame
                && supportCompression > 0f
                && groundHit.normal.y >= groundFlatNormalThreshold
                && verticalSpeed > 0f)
            {
                float planarSpd = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
                float ny = Mathf.Clamp(groundHit.normal.y, 0.05f, 1f);
                float expectedClimb =
                    planarSpd * Mathf.Sqrt(Mathf.Max(0f, 1f - ny * ny)) / ny;
                float maxUp = expectedClimb + seamHopMaxVerticalSpeed;
                if (verticalSpeed > maxUp)
                    verticalSpeed = maxUp;
            }

            float absAngle = Mathf.Abs(currentSignedAngleToDesired);
            float targetForwardSpeed = desiredMoveAmount * currentPlanarMax;

            if (isDriftingActive)
            {
                // DRIFT ATTIVO: target = retention*entry. Il floor planare che
                // sostiene la speed durante il 360 e' applicato in fondo a
                // UpdateVelocity (sulla velocity mondiale, non sulla componente
                // locale), cosi' non c'e' scatto di direzione all'ingresso.
                targetForwardSpeed = driftEntrySpeed * activeDriftForwardRetention;
            }
            else if (desiredMoveAmount <= 0.001f)
            {
                targetForwardSpeed = 0f;
            }
            else
            {
                if (isReorientingFromStop && absAngle > rotateBeforeMoveReleaseAngle)
                {
                    targetForwardSpeed = 0f;
                }
                else if (isReorientingWhileMoving && absAngle > movingReorientationExitAngle)
                {
                    float limitedTarget = desiredMoveAmount * currentPlanarMax * movingReorientationAccelerationFactor;
                    targetForwardSpeed = limitedTarget;
                }
            }

            float forwardRate =
                (Mathf.Abs(targetForwardSpeed) > Mathf.Abs(forwardSpeed))
                ? acceleration
                : deceleration;

            if (isReorientingWhileMoving && absAngle > movingReorientationExitAngle)
            {
                forwardRate = Mathf.Max(forwardRate, movingReorientationBrakeStrength);
            }

            if (CurrentBrake)
            {
                targetForwardSpeed = 0f;
                forwardRate = brakeStrength;
            }

            forwardSpeed = Mathf.MoveTowards(
                forwardSpeed,
                targetForwardSpeed,
                forwardRate * Time.fixedDeltaTime
            );

            // Grip laterale fusa per fase (camminata/corsa, vedi PesoCorsa).
            float lateralFriction = groundLateralFrictionCorrente;

            if (!IsGrounded)
            {
                lateralFriction = airLateralFrictionCorrente;
            }
            else if (isDriftingActive)
            {
                lateralFriction = activeDriftLateralFriction;
            }
            else if (IsDrifting)
            {
                lateralFriction = driftLateralFrictionCorrente;
            }

            float speedRatio = Mathf.Clamp01(Mathf.Abs(CurrentSpeed) / Mathf.Max(0.01f, currentEffectiveMax));

            // Il drift attivo ha la sua grip dedicata: lo slip multiplier del
            // "carrello della spesa" non deve intervenire (abbasserebbe di nuovo
            // la grip ulteriormente in modo non desiderato). Si applica solo
            // nel caso passivo.
            if (!isDriftingActive && IsGrounded && lastSteerAmount > shoppingCartSlipSteerThresholdCorrente)
            {
                float steerFactor = Mathf.InverseLerp(
                    shoppingCartSlipSteerThresholdCorrente,
                    1f,
                    lastSteerAmount
                );

                float slipMultiplier = Mathf.Lerp(
                    1f,
                    1f - shoppingCartSlipCorrente,
                    speedRatio * steerFactor
                );

                lateralFriction *= slipMultiplier;
            }

            lateralSpeed = Mathf.MoveTowards(
                lateralSpeed,
                0f,
                lateralFriction * Time.fixedDeltaTime
            );

            verticalSpeed -= gravity * Time.fixedDeltaTime;

            Vector3 finalVelocity;

            // Durante drift attivo: ROTAZIONE MONDIALE SELF-STABILIZING.
            // Invece di ricostruire la velocity da componenti locali (forward +
            // lateral) che e' intrinsecamente lenta a seguire il muso, ruotiamo
            // direttamente la velocity mondiale verso il forward del muso con
            // un rate che CRESCE con lo slip angle (auto-stabilizzante):
            //  -Piu' lo slip e' grande, piu' la velocity accelera verso il muso.
            //  -Lo slip non puo mai superare 90 gradi: quando |slip| > 90, la
            //   rotazione si limita a ridurre lo slip a 90 gradi (non a 0 = non
            //   retromarcia, non oltre 90 = niente tremare).
            //  -Niente 'va di lato' (la velocity raggiunge sempre il muso).
            //  -Niente 'retromarcia' (lo slip non supera 90 gradi).
            //  -Drift feel preservato: a basso slip la velocity segue lentamente.
            if (isDriftingActive && IsGrounded && !CurrentBrake)
            {
                Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                float velMag = velXZ.magnitude;

                if (velMag > 0.01f)
                {
                    Vector3 velDir = velXZ / velMag;
                    Vector3 fwdXZ = Vector3.Scale(transform.forward, new Vector3(1f, 0f, 1f));
                    if (fwdXZ.sqrMagnitude > 0.0001f) fwdXZ.Normalize();
                    else fwdXZ = transform.forward;

                    float slipSigned = Vector3.SignedAngle(fwdXZ, velDir, Vector3.up);
                    float slipDeg = Mathf.Abs(slipSigned);

                    // Rate self-stabilizing: cresce con lo slip angle.
                    // K (activeDriftSlipStabilizeK) controlla quanto slip prima
                    // che la velocity acceleri. K=30: a 30 gradi il rate raddoppia.
                    float rotRate = activeDriftLateralFriction
                        * (1f + slipDeg / activeDriftSlipStabilizeK)
                        * Mathf.Rad2Deg
                        * Time.fixedDeltaTime;

                    // Segno: rotAmount = -slipSigned ruota la velocity VERSO il muso
                    // (non lontano). Il segno di slipSigned e' l'angolo da muso a
                    // velocity: positivo significa velocity e' a +X (CCW) dal muso,
                    // per allinearla al muso dobbiamo ruotarla di -slipSigned.
                    // Cap |slip| <= 90: full follow (riduci a 0, ma non cross 0).
                    // Cap |slip| > 90: riduci a 90 (non a 0 = non retromarcia, non oltre 90).
                    float rotAmount;
                    if (slipDeg <= 90f)
                    {
                        rotAmount = -Mathf.Sign(slipSigned) * Mathf.Min(rotRate, slipDeg);
                    }
                    else
                    {
                        // Slip estremo: riduci a 90 gradi, non a 0 (evita retromarcia).
                        // maxRot = quanto possiamo ruotare per portare |slip| a 90.
                        float maxRotTo90 = Mathf.Max(0f, slipDeg - 90f);
                        rotAmount = -Mathf.Sign(slipSigned) * Mathf.Min(rotRate, maxRotTo90);
                    }

                    Vector3 newDir = Quaternion.Euler(0f, rotAmount, 0f) * velDir;

                    // Mantieni la magnitudine (forwardSpeed come target, floor applicato dopo)
                    float targetMag = Mathf.Max(velMag, forwardSpeed);
                    finalVelocity = new Vector3(
                        newDir.x * targetMag,
                        verticalSpeed,
                        newDir.z * targetMag
                    );
                }
                else
                {
                    // Velocity ~0: fallback al modello originale
                    finalVelocity =
                        transform.right * lateralSpeed +
                        transform.forward * forwardSpeed +
                        Vector3.up * verticalSpeed;
                }
            }
            else
            {
                // Non-drift o brake o air: modello originale (lateral damping)
                finalVelocity =
                    transform.right * lateralSpeed +
                    transform.forward * forwardSpeed +
                    Vector3.up * verticalSpeed;
            }

            Vector3 planarFinal = finalVelocity;
            planarFinal.y = 0f;
            float planarMax = currentPlanarMax;

            if (planarFinal.magnitude > planarMax)
            {
                planarFinal = planarFinal.normalized * planarMax;
                finalVelocity.x = planarFinal.x;
                finalVelocity.z = planarFinal.z;
            }

            if (WallContactActive)
            {
                float intoWall = Vector3.Dot(finalVelocity, steepWallNormal);
                if (intoWall < 0f)
                    finalVelocity -= steepWallNormal * intoWall;

                if (finalVelocity.y > 0f)
                    finalVelocity.y = 0f;
            }

            // FLOOR PLANARE durante drift attivo: mantiene la magnitudine della
            // velocity mondiale >= activeDriftMinForwardSpeed, nella DIREZIONE
            // ATTUALE (non forza il muso forward). Cosi':
            //  -Durante un 360 la velocity resta sostenuta anche quando il muso
            //   ruota e la componente locale forward oscilla: planarSpeed non
            //   scende sotto activeDriftMinSpeed e il drift NON esce piu' a meta'
            //   rotazione (niente piu' 'gira normalmente e rientra').
            //  -Niente scatto di DIREZIONE all'ingresso (a differenza del vecchio
            //   Mathf.Max sul forwardSpeed locale, che forzava la velocity
            //   lungo il muso appena reindirizzato). Qui si preserva la direzione
            //   della velocity attuale, solo la magnitudine viene tirata su.
            //  -NON piu' gated su WallContactActive: i lievi sfregamenti non
            //   spezzano piu' il drift. Il crash vero e' gestito da OnImpact
            //   (pendingCrashExit), non dalla velocita'.
            //  -Il Brake bypassa (frena normalmente).
            if (isDriftingActive && !CurrentBrake)
            {
                Vector3 planar = new Vector3(finalVelocity.x, 0f, finalVelocity.z);
                float planarMag = planar.magnitude;
                if (planarMag > 0.0001f && planarMag < activeDriftMinForwardSpeed)
                {
                    float scale = activeDriftMinForwardSpeed / planarMag;
                    finalVelocity.x *= scale;
                    finalVelocity.z *= scale;
                }
            }

            rb.linearVelocity = finalVelocity;
            lastSetVelocity = finalVelocity;

            Vector3 localFinal = transform.InverseTransformDirection(finalVelocity);
            CurrentSpeed = localFinal.z;

            if (Mathf.Abs(CurrentSpeed - lastReportedSpeed) > 0.05f)
            {
                lastReportedSpeed = CurrentSpeed;
                OnSpeedChanged?.Invoke(CurrentSpeed);
            }
        }

        private void UpdateDriftVisual()
        {
            if (driftVisual == null)
                return;

            float targetYaw = 0f;
            float lerpSpeed = driftVisualLerpSpeedCorrente;

            // DRIFT ATTIVO: inclinazione visiva basata sullo SLIP ANGLE
            // (angolo fra muso del kart e velocity), NON sul joystick.
            // Cosi' quando il corpo e' opposto al joystick (180 gradi) ma la
            // velocity segue il joystick, la visual yaw riflette la derapata
            // reale (muso opposto alla velocity = slip 180 gradi = visual yaw
            // massima). Il kart appare 'in derapata' rispetto alla direzione
            // di marcia, non 'opposto' come prima.
            // Fallback al joystick se velocity e' ~0 (fermo).
            // Lerp piu' veloce (activeDriftVisualLerpSpeed, default 12) per
            // far tornare la visual yaw a 0 rapidamente dopo un 360: niente
            // 'rimane inclinato' residuo.
            if (isDriftingActive)
            {
                Vector3 velXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                if (velXZ.sqrMagnitude > 0.5f)
                {
                    velXZ.Normalize();
                    Vector3 fwdXZ = Vector3.Scale(transform.forward, new Vector3(1f, 0f, 1f));
                    if (fwdXZ.sqrMagnitude > 0.0001f) fwdXZ.Normalize();
                    else fwdXZ = transform.forward;

                    float slipAngle = Vector3.SignedAngle(fwdXZ, velXZ, Vector3.up);
                    float steer = Mathf.Clamp(slipAngle / 90f, -1f, 1f);
                    targetYaw = steer * driftVisualYawDegreesCorrente * activeDriftVisualYawScale;
                }
                else if (desiredMoveDirection.sqrMagnitude > 0.001f)
                {
                    // Velocity ~0: fallback al joystick (vecchio comportamento).
                    float signedAngle = Vector3.SignedAngle(transform.forward, desiredMoveDirection, Vector3.up);
                    float steer = Mathf.Clamp(signedAngle / 90f, -1f, 1f);
                    targetYaw = steer * driftVisualYawDegreesCorrente * activeDriftVisualYawScale;
                }

                lerpSpeed = activeDriftVisualLerpSpeed;
            }
            else if (IsDrifting && desiredMoveDirection.sqrMagnitude > 0.001f)
            {
                float signedAngle = Vector3.SignedAngle(transform.forward, desiredMoveDirection, Vector3.up);
                float steer = Mathf.Clamp(signedAngle / 90f, -1f, 1f);
                targetYaw = steer * driftVisualYawDegreesCorrente;
            }

            currentDriftYaw = Mathf.Lerp(
                currentDriftYaw,
                targetYaw,
                1f - Mathf.Exp(-lerpSpeed * Time.deltaTime)
            );
        }

        private void UpdateGroundAlignmentVisual()
        {
            if (driftVisual == null)
                return;

            bool hasFL = TryGetGroundPoint(frontLeftGroundProbe, out Vector3 fl);
            bool hasFR = TryGetGroundPoint(frontRightGroundProbe, out Vector3 fr);
            bool hasRL = TryGetGroundPoint(rearLeftGroundProbe, out Vector3 rl);
            bool hasRR = TryGetGroundPoint(rearRightGroundProbe, out Vector3 rr);

            int hitCount = 0;
            if (hasFL) hitCount++;
            if (hasFR) hitCount++;
            if (hasRL) hitCount++;
            if (hasRR) hitCount++;

            Vector3 targetUp = hasValidGroundUp ? lastValidGroundUp : Vector3.up;

            if (hitCount == 4)
            {
                Vector3 frontMid = (fl + fr) * 0.5f;
                Vector3 rearMid = (rl + rr) * 0.5f;
                Vector3 leftMid = (fl + rl) * 0.5f;
                Vector3 rightMid = (fr + rr) * 0.5f;

                Vector3 groundForward = (frontMid - rearMid).normalized;
                Vector3 groundRight = (rightMid - leftMid).normalized;

                if (groundForward.sqrMagnitude >= 0.001f && groundRight.sqrMagnitude >= 0.001f)
                {
                    Vector3 groundUp = Vector3.Cross(groundForward, groundRight).normalized;

                    if (groundUp.y < 0f)
                        groundUp = -groundUp;

                    lastValidGroundUp = groundUp;
                    hasValidGroundUp = true;
                    targetUp = groundUp;
                }
            }


            // ===== Lancio skate: volo parabolico balistico =====
            // Il PITCH locale (rotazione X) resta FISSO per tutto il volo
            // (valore catturato al primo contatto col muro, ereditato dalla
            // rampa slopeata appena percorsa). Non segue la velocity nemmeno
            // durante la salita sul muro. Solo lo YAW e' autorizzato a cambiare
            // in discesa (il muso vira verso la direzione orizzontale della
            // velocity). L'up resta sempre Vector3.up: niente bank, niente roll.
            //
            // Il pitchedForward e' costruito in WORLD SPACE (planarDir*cos +
            // Vector3.up*sin), cosi' non dipende dalla direzione del right
            // locale: quando lo yaw si gira di ~180 in discesa (la velocity
            // planare si inverte) il muso resta inclinato VERSO L'ALTO di
            // frozenPitch gradi, come uno skate che ridiscende il vert.
            // ===== Lancio skate: volo parabolico "guidando sul muro" =====
            // Il kart resta visivamente ATTACCATO alla parete verticale per tutto
            // il volo, come se stesse guidando sulla superficie del muro:
            //  - UP = launchWallNormal (la normale del muro catturata al primo
            //    contatto, punta verso l'esterno della parete). Le "4 ruote"
            //    restano premute sul muro.
            //  - FORWARD = direzione del moto PROIETTATA sul piano del muro.
            //    In questo modo il muso traccia la parabola UM (su per il vert,
            //    oltre il top, giu in discesa) ruotando gradualmente verso la
            //    destinazione, ma senza mai staccarsi dalla parete.
            // Nessun bank/roll: l'up e' fisso (la parete), solo il forward
            // cambia per seguire la tangente della traiettoria.
            if (skateRampLaunch)
            {
                targetUp = launchWallNormal;

                bool stillTouchingWall =
                    (Time.time - lastSkateRampContactTime) <= wallContactGraceTime;
                Vector3 vel = stillTouchingWall ? launchVelocity : rb.linearVelocity;

                // Proietta la velocity sul piano del muro (perpendicolare alla
                // sua normale): questo e' il "forward" che il kart segue mentre
                // "guida" sulla superficie della parete, tracciando la parabola.
                Vector3 forward = Vector3.ProjectOnPlane(vel, targetUp);
                if (forward.sqrMagnitude < 0.001f)
                {
                    // Velocity quasi parallela alla normale del muro (caso raro):
                    // fallback al forward attuale del visual per evitare LookRotation
                    // degenere.
                    forward = driftVisual.forward;
                    // Proiettiamo anche il fallback sul piano del muro.
                    forward = Vector3.ProjectOnPlane(forward, targetUp);
                    if (forward.sqrMagnitude < 0.001f)
                        forward = Vector3.Cross(targetUp, Vector3.right);
                }
                forward.Normalize();

                Quaternion launchTarget = Quaternion.LookRotation(forward, targetUp);

                if (!hasVisualWorldRotation)
                {
                    visualWorldRotation = launchTarget;
                    hasVisualWorldRotation = true;
                }
                // Rate-limit angolare costante (gradi/sec) invece di Slerp:
                // il muso rincorre la traiettoria parabolica a velocita'
                // uniforme, come fa visualYawMaxTurnSpeed sullo sterzo. Piu'
                // fluido e meno "snappy" quando la velocity cambia di colpo
                // (es. impatto col muro dopo una curva di avvicinamento).
                float maxDegStep = skateRampVisualTurnSpeed * Time.deltaTime;
                visualWorldRotation = Quaternion.RotateTowards(
                    visualWorldRotation,
                    launchTarget,
                    maxDegStep
                );
                driftVisual.rotation = visualWorldRotation;
                return;
            }

            // ===== Comportamento normale (drift / inversioni / allineamento) =====
            // Durante le inversioni il corpo ruota a scatti (fisica voluta):
            // il muso invece punta la direzione di sterzo e ci arriva a velocita'
            // costante (visualYawMaxTurnSpeed), senza scatti ne' trascinamenti.
            // In drift resta agganciato al corpo per preservare il visual del drift.
            float targetYaw =
                !IsDrifting && desiredMoveDirection.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(desiredMoveDirection.x, desiredMoveDirection.z) * Mathf.Rad2Deg
                    : transform.eulerAngles.y;

            if (!hasVisualYawDegrees)
            {
                visualYawDegrees = targetYaw;
                hasVisualYawDegrees = true;
            }
            visualYawDegrees = Mathf.MoveTowardsAngle(
                visualYawDegrees,
                targetYaw,
                visualYawMaxTurnSpeed * Time.deltaTime
            );

            Vector3 yawForward =
                Quaternion.Euler(0f, visualYawDegrees, 0f) *
                driftVisualBaseRotation *
                Quaternion.Euler(0f, currentDriftYaw, 0f) *
                Vector3.forward;

            Vector3 projectedForward = Vector3.ProjectOnPlane(yawForward, targetUp);


            projectedForward = projectedForward.normalized;

            if (projectedForward.sqrMagnitude < 0.001f)
                projectedForward = Vector3.ProjectOnPlane(transform.forward, targetUp).normalized;

            if (projectedForward.sqrMagnitude < 0.001f)
                projectedForward = driftVisual.forward;

            Quaternion targetWorldRotation = Quaternion.LookRotation(projectedForward, targetUp);

            // grafica e' figlia del corpo: ad ogni FixedUpdate il padre la trascina
            // con la propria rotazione (nelle inversioni anche ~50 gradi/step).
            // Se il slerp parte dal valore gia' trascinato, l'errore resta e si vede
            // come uno scatto. Partiamo invece da uno stato nostro: la scrittura
            // annulla il trascinamento completamente ad ogni frame.
            if (!hasVisualWorldRotation)
            {
                visualWorldRotation = targetWorldRotation;
                hasVisualWorldRotation = true;
            }
            visualWorldRotation = Quaternion.Slerp(
                visualWorldRotation,
                targetWorldRotation,
                1f - Mathf.Exp(-groundAlignLerpSpeed * Time.deltaTime)
            );
            driftVisual.rotation = visualWorldRotation;
        }

        private bool TrySphereCastGround(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float distance,
            out RaycastHit bestHit
        )
        {
            bestHit = default;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                radius,
                direction,
                distance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            float bestWalkableDistance = float.MaxValue;
            bool found = false;


            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.root == transform.root)
                    continue;

                // Hit a distanza ~zero = la sfera PARTIVA gia' sovrapposta al
                // collider: NON e' terreno sotto i piedi, e' un piano di
                // LATO/SOPRA il kart. Caso tipico: durante la scalata della
                // parete skate il centro del kart passa alla quota della
                // piattaforma piana in cima (d = 0) e lo SphereCast la
                // becca -> falso IsGrounded -> il lancio esce a meta' salita
                // e il clamp anti ghost-bump uccide la vy. Un vero supporto
                // ha sempre della corsa (d ~0.05-0.15 a riposo).
                if (hit.distance <= 0.01f)
                    continue;

                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                bool walkable = slopeAngle <= maxGroundSlopeAngle;

                if (!walkable)
                    continue;

                if (hit.distance < bestWalkableDistance)
                {
                    bestWalkableDistance = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool TryGetGroundPoint(Transform probe, out Vector3 point)
        {
            point = Vector3.zero;

            if (probe == null)
                return false;

            RaycastHit[] hits = Physics.RaycastAll(
                probe.position,
                Vector3.down,
                visualGroundAlignDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.root == transform.root)
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    point = hit.point;
                    found = true;
                }
            }

            return found;
        }

        private void DrawProbeGizmo(Transform probe)
        {
            if (probe == null)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                probe.position,
                probe.position + Vector3.down * visualGroundAlignDistance
            );
            Gizmos.DrawWireSphere(
                probe.position + Vector3.down * visualGroundAlignDistance,
                0.04f
            );
        }

        private void StartMultiplier(float value, float duration)
        {
            if (multiplierRoutine != null)
                StopCoroutine(multiplierRoutine);

            multiplierRoutine = StartCoroutine(MultiplierRoutine(value, duration));
        }

        private IEnumerator MultiplierRoutine(float value, float duration)
        {
            speedMultiplier = value;
            yield return new WaitForSeconds(duration);
            speedMultiplier = 1f;
            multiplierRoutine = null;
        }

        #endregion
    }
}
