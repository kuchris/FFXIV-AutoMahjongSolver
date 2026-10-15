namespace MahjongXiv.Core;

internal static class MjConstants
{
    public const string PrimaryCommand = "/mj";
    public const string AliasCommand = "/mahjongxiv";

    public const string LogPrefix = "[MJ]";

    /// <summary>
    /// Internal addon names for the Doman Mahjong game UI, tried in order. Casual tables open "Emj";
    /// "EmjL" was seen live on a ranked table (ContentFinderCondition 766, 2026-10-14) and appears to
    /// be the ranked variant. Hand node ids read correctly on both, and a manual EmjL capture showed
    /// the same state codes and opcodes as Emj (discard 15/7, call 11 with labels from index 7,
    /// advance 17 at 32 and 14 at 29, new-hand 9 at 2).
    /// </summary>
    public static readonly string[] EmjAddonNames = ["Emj", "EmjL"];

    /// <summary>
    /// Player closed-hand tile node ids, left to right (community RE, last verified ~patch 7.2).
    /// Index 13 is the freshly drawn tile. These MUST be re-verified live with /mj dump.
    /// </summary>
    public static readonly uint[] HandTileNodeIds =
        [134, 1340001, 1340002, 1340003, 1340004, 1340005, 1340006, 1340007, 1340008, 1340009, 1340010, 1340011, 1340012];

    public const uint DrawnTileNodeId = 135;

    /// <summary>Within a hand-tile component: the button is NodeList[0]; its image is NodeList[ImageNodeListIndex].</summary>
    public const int HandButtonNodeListIndex = 0;
    public const int HandImageNodeListIndex = 4;

    /// <summary>
    /// Call / self-action popup button labels live in consecutive string AtkValues starting here; the
    /// value to fire for a button is its position in that run, i.e. <c>[11, atkValueIndex - this]</c>.
    /// Verified live: a [7]=Kan / [8]=Pon / [9]=Pass popup fired [11,0] when Kan was clicked.
    /// </summary>
    public const int ActionButtonBaseIndex = 7;

    /// <summary>
    /// The local player's called-meld slots (verified live: our 2s pon decoded under 112). The meld
    /// area (node 111) holds one four-slot strip per seat in panel order: self=112 family,
    /// 113/114/115 families for the other seats (e.g. panel 42's pon decoded under 114 in two games).
    /// </summary>
    public static readonly uint[] MeldGroupNodeIds = [112, 1120001, 1120002, 1120003];

    /// <summary>Depth bound for the recursive tile walk over a meld container, guarding against cycles.</summary>
    public const int NodeWalkMaxDepth = 12;

    /// <summary>
    /// Discard-river containers and their first tile slot, verified live (board survey 2026-06-12).
    /// Slot k>0 has node id FirstSlot*10000+k, same scheme as the hand tiles. Index 0 is the local
    /// player's river (confirmed: our discarded 6s appeared at 1180006). The other three pair with
    /// <see cref="PlayerPanelNodeIds"/> by index — that pairing still needs live confirmation.
    /// </summary>
    public static readonly (uint Container, uint FirstSlot)[] RiverNodeIds =
        [(116, 118), (119, 121), (122, 124), (125, 127)];

    public const int SelfIndex = 0;
    public const int RiverMaxSlots = 24;

    /// <summary>Dora indicator group; the indicator tile image sits in its subtree (21>26>28).</summary>
    public const uint DoraGroupNodeId = 21;

    /// <summary>
    /// Player info panels (name, seat wind, score, Riichi!/Pon! badge), one per seat.
    /// Verified live: 38 = local player ("South", riichi badge appeared here on our own riichi).
    /// </summary>
    public static readonly uint[] PlayerPanelNodeIds = [38, 40, 42, 44];

    /// <summary>
    /// The three opponents' meld-strip slots (113/114/115 families under meld area 111). Tiles found
    /// here only feed the seen-tile counts; our own strip (112) is excluded because the hand snapshot
    /// already contributes our melds.
    /// </summary>
    public static readonly uint[] OpponentMeldAreaNodeIds =
    [
        113, 1130001, 1130002, 1130003,
        114, 1140001, 1140002, 1140003,
        115, 1150001, 1150002, 1150003,
    ];
}
