// =============================================================================
// KartSkidmarks
// -----------------------------------------------------------------------------
// COSA FA:
//   Accende e spegne le "sgommate" del kart: una serie di TrailRenderer (segni
//   neri sull'asfalto) che seguono IsSkidding (drift attivo o passivo, ma NON
//   durante la finestra del boost-reward del drift), e ParticleSystem (fumo
//   bianco) che si attivano solo a boost completamente carico (IsDriftCharged).
// COME SI USA:
//   1) Aggiungi questo componente da qualche parte sotto il kart (es. su un
//      figlio "VFX"). 2) Trascina il KartController nello slot "Controller"
//      (se lo lasci vuoto cerca quello del genitore in automatico).
//   3) Crea due figli vuoti sotto le ruote posteriori, mettici un
//      TrailRenderer ciascuno (Material scuro, Width ~0.1, Time ~3s) e
//      trascinali nell'array "Trails". 4) (Opzionale) stessa cosa per il fumo
//      con ParticleSystem nell'array "Smoke".
// DA SAPERE:
//   - I TrailRenderer vanno tenuti con "Emitting" disattivato di default
//     nell'Inspector: ci pensa questo script ad accenderli quando serve.
//   - I ParticleSystem vanno lasciati in "Play On Awake = true" ma con il
//     modulo Emission disattivato all'inizio (lo script lo riattiva al volo).
//     Il fumo si accende SOLO quando la carica del drift attivo e' completa
//     (IsDriftCharged true), non durante tutto il drift.
// =============================================================================

using ArcadeKart.Core;
using UnityEngine;

namespace ArcadeKart.Utility
{
    public class KartSkidmarks : MonoBehaviour
    {
        #region Inspector

        [SerializeField, Tooltip("Il KartController da cui leggere IsDrifting. Se null, lo cerca nel parent.")]
        private KartController controller;

        [SerializeField, Tooltip("TrailRenderer delle sgommate (uno per ruota posteriore, di solito).")]
        private TrailRenderer[] trails;

        [SerializeField, Tooltip("ParticleSystem opzionali per il fumo delle gomme.")]
        private ParticleSystem[] smoke;

        #endregion

        #region Unity callbacks

        private void Reset()
        {
            // Comodita' in editor: appena aggiungi il componente, prova a riempire il riferimento.
            controller = GetComponentInParent<KartController>();
        }

        private void Awake()
        {
            if (controller == null)
                controller = GetComponentInParent<KartController>();
            if (controller == null)
                Debug.LogWarning("[KartSkidmarks] Nessun KartController trovato: la sgommata non si attivera'.", this);

            ApplyTrails(false);
            ApplySmoke(false);
        }

        private void LateUpdate()
        {
            if (controller == null) return;
            ApplyTrails(controller.IsSkidding);
            ApplySmoke(controller.IsDriftCharged);
        }

        #endregion

        #region Internal

        private bool lastTrailState;
        private bool trailsInitialized;

        private bool lastSmokeState;
        private bool smokeInitialized;

        private void ApplyTrails(bool drifting)
        {
            if (trailsInitialized && drifting == lastTrailState) return;
            trailsInitialized = true;
            lastTrailState = drifting;

            if (trails == null) return;
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null) trails[i].emitting = drifting;
            }
        }

        private void ApplySmoke(bool charged)
        {
            if (smokeInitialized && charged == lastSmokeState) return;
            smokeInitialized = true;
            lastSmokeState = charged;

            if (smoke == null) return;
            for (int i = 0; i < smoke.Length; i++)
            {
                if (smoke[i] == null) continue;
                var em = smoke[i].emission;
                em.enabled = charged;
            }
        }

        #endregion
    }
}
