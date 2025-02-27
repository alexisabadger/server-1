﻿using Hybrasyl.Dialogs;
using Hybrasyl.Interfaces;
using Hybrasyl.Objects;
using Hybrasyl.Xml.Objects;
using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;

namespace Hybrasyl.Scripting;

// We implement IInteractable here so that scripting can just work
[MoonSharpUserData]
public class HybrasylItemObject : HybrasylWorldObject, IInteractable
{
    public HybrasylItemObject(ItemObject obj) : base(obj) { }
    internal ItemObject Item => WorldObject as ItemObject;
    public double Durability => Item.Durability;
    public uint MaximumDurability => Item.MaximumDurability;
    public int Weight => Item.Weight;
    public int Value => (int)Item.Value;
    public StatInfo Stats => Item.Stats;
    public List<string> Categories => Item.Categories;
    public int MinLevel => Item.Template.Properties?.Restrictions?.Level?.Min ?? 1;
    public int MaxLevel => Item.Template.Properties?.Restrictions?.Level?.Max ?? 1;
    public string Description => Item.Template.Properties?.Vendor?.Description ?? string.Empty;

    // ItemFlags exposed here, to make it easier to use in scripting
    public bool Bound => Item.Template.Properties.Flags.HasFlag(ItemFlags.Bound);
    public bool Depositable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Depositable);
    public bool Enchantable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Enchantable);
    public bool Consecratable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Consecratable);
    public bool Tailorable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Tailorable);
    public bool Smithable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Smithable);
    public bool Exchangeable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Exchangeable);
    public bool Vendorable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Vendorable);
    public bool Perishable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Perishable);
    public bool UniqueInventory => Item.Template.Properties.Flags.HasFlag(ItemFlags.UniqueInventory);
    public bool MasterOnly => Item.Template.Properties.Flags.HasFlag(ItemFlags.MasterOnly);
    public bool UniqueEquipped => Item.Template.Properties.Flags.HasFlag(ItemFlags.UniqueEquipped);
    public bool Identifiable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Identifiable);
    public bool Undamageable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Undamageable);
    public bool Consumable => Item.Template.Properties.Flags.HasFlag(ItemFlags.Consumable);

    public ushort Sprite
    {
        get => Item.Sprite;
        set => throw new NotImplementedException();
    }

    public Script Script => Item.Script;

    public List<DialogSequence> DialogSequences
    {
        get => Item.DialogSequences;
        set => throw new NotImplementedException();
    }

    public bool AllowDead => Item.AllowDead;
    public uint Id => Item.Id;

    public Dictionary<string, DialogSequence> SequenceIndex
    {
        get => Item.SequenceIndex;
        set => throw new NotImplementedException();
    }

    public ushort DialogSprite => Item.DialogSprite;
}