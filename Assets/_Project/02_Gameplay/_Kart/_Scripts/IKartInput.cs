using UnityEngine;

namespace ArcadeKart.Core
{
    // Sorgente di input astratta per un kart. Il KartController legge i suoi
    // valori tramite questa interfaccia invece di dipendere direttamente da
    // KartInput, cosi' un kart puo' essere pilotato dal giocatore (KartInput)
    // o dalla CPU (EnemyKart) mantenendo identica la fisica di guida.
    // Il kart del giocatore resta invariato: KartInput implementa IKartInput
    // e continua a leggere l'Input System; il NPC usa EnemyKart che lo
    // implementa calcolando move/brake/drift dalla propria AI.
    public interface IKartInput
    {
        // Input di movimento 2D: x = sterzo, y = accelerazione/retromarcia.
        // Spazio "input" (camera-relativo per il giocatore, ma per il NPC
        // viene comunque espresso in spazio camera per essere consistente con
        // la conversione camera-relativa che fa il KartController). Magnitudo
        // 0..1: 1 = full throttle verso la direzione indicata.
        Vector2 Move { get; }

        // True mentre il freno e' tenuto premuto.
        bool Brake { get; }

        // True mentre il tasto drift e' tenuto premuto (sgommata intenzionale).
        bool Drift { get; }

        // True mentre il tasto boost e' tenuto premuto (mouse sx per il
        // giocatore): alza la velocita' massima da cruiseSpeed a maxSpeed.
        bool Boost { get; }

        // True solo nel frame di pressione del tasto reset/respawn.
        bool ResetPressed { get; }
    }
}
