using UnityEngine;

namespace ArcadeKart.Gameplay
{
    /// <summary>
    /// Anima uno SpriteRenderer scorrendo in loop i quadri di un foglio sprite (spritesheet),
    /// un'immagine unica che contiene piu' quadri affiancati su una griglia di colonne x righe.
    /// I quadri vengono ritagliati una sola volta in Awake con Sprite.Create, prendendo la
    /// texture dallo sprite assegnato nel renderer; a ogni Update si mostra il quadro successivo.
    /// Non tocca il transform: far guardare il quadro verso la camera resta compito di SpriteBillboard,
    /// cosi' i due componenti non si pestano i piedi (uno anima lo sprite, l'altro ruota l'oggetto).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimata : MonoBehaviour
    {
        [Header("Griglia del foglio")]
        [Tooltip("Numero di colonne del foglio (quadri affiancati orizzontalmente).")]
        [SerializeField, Range(1, 32)] private int colonne = 4;

        [Tooltip("Numero di righe del foglio (quadri impilati verticalmente).")]
        [SerializeField, Range(1, 32)] private int righe = 1;

        [Tooltip("Quadri da mostrare, contati da sinistra a destra e dall'alto in basso. Se 0 usa tutta la griglia.")]
        [SerializeField, Min(0)] private int quadriUsati = 4;

        [Header("Animazione")]
        [Tooltip("Quanti quadri mostrare al secondo: 10 = un giro completo del foglio in 0,4 secondi con 4 quadri.")]
        [SerializeField, Min(0.01f)] private float quadriAlSecondo = 10f;

        // Quadri ritagliati dal foglio in Awake, in ordine di scorrimento.
        private Sprite[] quadri;

        // SpriteRenderer di questo oggetto (giu' garantito da RequireComponent).
        private SpriteRenderer spriteRenderer;

        // Tempo accumulato in "quadri mostrati": l'indice corrente e' la sua parte intera.
        private float fase;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            Sprite foglio = spriteRenderer.sprite;
            if (foglio == null)
            {
                Debug.LogWarning("[SpriteAnimata] Nessuno sprite assegnato sul renderer: impossibile ritagliare i quadri.", this);
                return;
            }

            CostruisciQuadri(foglio);
        }

        // Ritaglia i quadri dal foglio: celle della griglia lette da sinistra a destra e dall'alto in basso.
        private void CostruisciQuadri(Sprite foglio)
        {
            Texture2D texture = foglio.texture;

            // La dimensione di una cella in pixel e il suo fattore di scala (pixel -> unita')
            // vengono presi dal foglio assegnato, cosi' lo sprite resta della stessa grandezza.
            float larghezzaQuadro = texture.width / colonne;
            float altezzaQuadro = texture.height / righe;
            float pixelPerUnita = foglio.pixelsPerUnit;

            int totale = quadriUsati > 0 ? Mathf.Min(quadriUsati, colonne * righe) : colonne * righe;
            quadri = new Sprite[totale];

            for (int i = 0; i < totale; i++)
            {
                int colonna = i % colonne;
                // La prima riga del foglio e' quella in alto: nelle coordinate texture (origine in basso) e' l'ultima.
                int riga = righe - 1 - (i / colonne);

                Rect ritaglio = new Rect(colonna * larghezzaQuadro, riga * altezzaQuadro, larghezzaQuadro, altezzaQuadro);
                quadri[i] = Sprite.Create(texture, ritaglio, new Vector2(0.5f, 0.5f), pixelPerUnita);
            }
        }

        private void Update()
        {
            if (quadri == null || quadri.Length == 0)
                return;

            fase += Time.deltaTime * quadriAlSecondo;
            int indice = Mathf.FloorToInt(fase) % quadri.Length;
            spriteRenderer.sprite = quadri[indice];
        }
    }
}
