// =============================================================================
// KartInput
// -----------------------------------------------------------------------------
// COSA FA:
//   Legge gli input del giocatore tramite il nuovo Input System di Unity e li
//   espone come proprieta' semplici (Move, Brake, ResetPressed) che il
//   KartController puo' usare senza preoccuparsi di come arriva l'input.
//
// COME SI USA:
//   1) Aggiungi questo componente sullo stesso GameObject del kart.
//   2) Trascina nei tre slot dell'Inspector le InputActionReference che
//      corrispondono a movimento, freno e reset (puoi crearle nel tuo
//      .inputactions asset). Esempio: Move = WASD/stick, Brake = Spazio,
//      Reset = R.
//   3) Le action vengono attivate automaticamente in OnEnable.
//
// DA SAPERE:
//   - Se uno slot resta vuoto, il componente stampa un Warning ma NON va in
//     errore: l'input corrispondente sara' semplicemente sempre "fermo".
//   - ResetPressed e' true SOLO nel frame in cui premi il tasto, perfetto per
//     attivazioni "una volta sola" tipo respawn.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ArcadeKart.Core
{
    // Implementa IKartInput: cosi' il KartController puo' leggere l'input da
    // una qualsiasi sorgente (giocatore via KartInput, CPU via EnemyKart)
    // senza dipendere dal tipo concreto. La logica resta invariata: legge
    // ancora l'Input System nelle stesse proprieta' di prima.
    public class KartInput : MonoBehaviour, IKartInput
    {
        #region Inspector

        // PER MODIFICARE: trascina qui le tue InputActionReference dal file .inputactions del progetto.
        // Per crearne di nuove o cambiare i tasti: doppio-click sul file .inputactions e usa l'editor di Unity.
        [SerializeField, Tooltip("Action di movimento (Vector2). Esempio: WASD o stick analogico sinistro.")]
        private InputActionReference moveAction;

        [SerializeField, Tooltip("Action di freno (Button). Esempio: tasto Spazio o pulsante South del gamepad.")]
        private InputActionReference brakeAction;

        // PER MODIFICARE: drift e' OPZIONALE. Se lasci lo slot vuoto, il kart usa solo il drift "passivo"
        // (driftFactor del KartController). Assegnalo se vuoi che il giocatore possa sgommare a comando.
        [SerializeField, Tooltip("Action di drift (Button, hold). Esempio: Shift sinistro o trigger destro del gamepad.")]
        private InputActionReference driftAction;

        [SerializeField, Tooltip("Action di reset/respawn (Button). Esempio: tasto R o Start del gamepad.")]
        private InputActionReference resetAction;

        #endregion

        #region Events

        // Evento opzionale: viene invocato quando il giocatore preme il tasto Reset.
        // Utile se vuoi collegare il respawn da Inspector senza scrivere codice.
        public UnityEvent OnResetRequested;

        #endregion

        #region Public API

        /// <summary>Current 2D move vector (x = steering, y = throttle/reverse).</summary>
        public Vector2 Move { get; private set; }

        /// <summary>True while the brake button is held down.</summary>
        public bool Brake { get; private set; }

        /// <summary>True while the drift button is held down (per "sgommata" intenzionale).</summary>
        public bool Drift { get; private set; }

        // True mentre il tasto boost (mouse sinistro) e' tenuto premuto.
        // Letto direttamente dal device Mouse (vedi Update): niente action da
        // cablare nella scena, il mouse e' l'unico device che lo attiva.
        public bool Boost { get; private set; }

        /// <summary>True only on the single frame the reset button is pressed.</summary>
        public bool ResetPressed { get; private set; }

        #endregion

        #region Unity callbacks

        private void OnEnable()
        {
            // Abilitiamo ogni action solo se e' stata effettivamente assegnata.
            // Se manca, segnaliamo con un warning chiaro: l'utente capisce subito
            // quale slot dell'Inspector e' rimasto vuoto.
            if (moveAction != null) moveAction.action.Enable();
            else Debug.LogWarning("[KartInput] Move action non assegnata su " + name + ".", this);

            if (brakeAction != null) brakeAction.action.Enable();
            else Debug.LogWarning("[KartInput] Brake action non assegnata su " + name + ".", this);

            // Drift e' opzionale: se non lo assegni il kart resta col drift "passivo" originale.
            if (driftAction != null) driftAction.action.Enable();

            if (resetAction != null) resetAction.action.Enable();
            else Debug.LogWarning("[KartInput] Reset action non assegnata su " + name + ".", this);
        }

        private void OnDisable()
        {
            // Disabilitiamo le action quando il componente non e' attivo.
            // Evita che gli input continuino a essere processati a gioco in pausa.
            if (moveAction != null) moveAction.action.Disable();
            if (brakeAction != null) brakeAction.action.Disable();
            if (driftAction != null) driftAction.action.Disable();
            if (resetAction != null) resetAction.action.Disable();
        }

        private void Update()
        {
            // Leggiamo i valori di input UNA volta per frame e li conserviamo.
            // Cosi' il KartController non deve sapere niente dell'Input System.
            Move = (moveAction != null) ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            Brake = (brakeAction != null) && brakeAction.action.IsPressed();
            Drift = (driftAction != null) && driftAction.action.IsPressed();

            // Boost: tasto sinistro del mouse. Letto diretto dal device
            // (Mouse.current e' null se non c'e' mouse -> false). Gated dal
            // KartController via ControlsEnabled, come Move/Brake/Drift.
            Boost = Mouse.current != null && Mouse.current.leftButton.isPressed;

            // WasPressedThisFrame e' true solo nel frame della pressione:
            // perfetto per evitare di chiamare il respawn ogni frame.
            ResetPressed = (resetAction != null) && resetAction.action.WasPressedThisFrame();

            // Inoltriamo l'evento Reset una sola volta (solo nel frame di pressione).
            if (ResetPressed) OnResetRequested?.Invoke();
        }

        #endregion
    }
}
