using UnityEngine;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Fa compiere allo sprite di questo oggetto (per Base2D: il busto del personaggio) un mezzo giro
    /// di 180 gradi a scatti, a intervallo fisso e di continuo: appare sottosopra nel piano dello schermo.
    /// Lo realizza con SpriteRenderer.flipX e flipY attivi insieme (= 180 gradi nel piano schermo),
    /// quindi NON tocca nessun transform: zero conflitti con SpriteBillboard (rotazione) e BobBusto (posizione),
    /// e le ancore figlie restano al loro posto (il flip e' puramente visivo).
    /// In editor lo sprite resta normale: il flip gira solo in Play.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FlipBusto : MonoBehaviour
    {
        [Header("Flip del busto")]
        [Tooltip("Secondi tra un mezzo giro (180 gradi) e l'altro.")]
        [SerializeField] private float intervalloFlip = 2f;

        // Riferimenti e stato del flip.
        private SpriteRenderer sprite;
        private float timer;
        private bool girato;

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (sprite == null) return;

            // Accumula il tempo e alterna lo stato a ogni intervallo compiuto.
            timer += Time.deltaTime;
            if (timer >= intervalloFlip)
            {
                timer -= intervalloFlip;
                girato = !girato;
            }

            // 180 gradi nel piano dello schermo = specchio orizzontale + verticale insieme.
            // Riassegnato ogni frame: lo stato resta coerente anche se qualcos'altro tocca i flip.
            sprite.flipX = girato;
            sprite.flipY = girato;
        }
    }
}
