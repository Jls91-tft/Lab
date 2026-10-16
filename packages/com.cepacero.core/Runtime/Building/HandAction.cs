using CepaCero.Items;

namespace CepaCero.Building
{
    /// <summary>What a mouse click does with the item in the hand (D-022: "la mano decide el clic").</summary>
    public enum HandAction : byte
    {
        None,
        MeleeAttack,
        RangedAttack,

        /// <summary>Hit the block under the cursor (axe, pickaxe, shovel, bare hands).</summary>
        HitBlock,
        Place,

        /// <summary>Take a piece down and get part of its cost back.</summary>
        Dismantle,

        /// <summary>Open the door, chest or workbench under the cursor.</summary>
        Interact,

        /// <summary>Eat, drink, bandage, analyse.</summary>
        UseItem,
    }

    /// <summary>The target of a click, as the client knows it.</summary>
    public readonly struct ClickTarget
    {
        public ClickTarget(bool hasBlock, bool isInteractive, bool hasPieceOfMine)
        {
            HasBlock = hasBlock;
            IsInteractive = isInteractive;
            HasPieceOfMine = hasPieceOfMine;
        }

        /// <summary>A structure or object stands in the cell.</summary>
        public bool HasBlock { get; }

        /// <summary>A door, chest or workbench: the right button opens it.</summary>
        public bool IsInteractive { get; }

        /// <summary>A construction piece (not a map building) that the hammer or a material click can take down.</summary>
        public bool HasPieceOfMine { get; }
    }

    public static class HandResolver
    {
        /// <summary>Decides the action of the primary (left) or secondary (right) button.</summary>
        public static HandAction Resolve(ItemDefinition held, bool buildsPieces, in ClickTarget target, bool secondary)
        {
            if (secondary)
            {
                if (target.IsInteractive)
                {
                    return HandAction.Interact;
                }
                return buildsPieces && target.HasPieceOfMine ? HandAction.Dismantle : HandAction.None;
            }
            if (buildsPieces)
            {
                return HandAction.Place;
            }
            if (held == null)
            {
                return target.HasBlock ? HandAction.HitBlock : HandAction.MeleeAttack;
            }
            if (held.tool_kind == "hammer" && target.HasPieceOfMine)
            {
                return HandAction.Dismantle;
            }
            if (!string.IsNullOrEmpty(held.tool_kind) && target.HasBlock)
            {
                return HandAction.HitBlock;                              // axes, picks and shovels work on blocks first, then fight
            }
            if (held.is_weapon)
            {
                return held.IsRanged ? HandAction.RangedAttack : HandAction.MeleeAttack;
            }
            if (!string.IsNullOrEmpty(held.tool_kind))
            {
                return HandAction.None;
            }
            if (held.heal > 0 || held.food > 0 || held.water > 0 || held.category == "sample")
            {
                return HandAction.UseItem;
            }
            return HandAction.None;
        }
    }
}
