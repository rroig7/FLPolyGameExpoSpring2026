using Godot;
using System;

[GlobalClass]
public partial class UpgradeInfo : Node
{
    [Export] public string UpgradeName;
    [Export] public int Cost;
    [Export] public float modifier;
    [Export] public float minValue;
    [Export] Label NameLabel;
    [Export] Button UpgradeButton;
    [Export] public string UpgradeDescription;

    [Export] public int MaxPurchases = 10;
    public int PurchaseCount { get; private set; } = 0;

    [Signal] public delegate void UpgradeSelectedEventHandler(UpgradeInfo upgrade);

    public override void _Ready() {
        UpgradeButton.Text = "XP: " + Cost.ToString();
        NameLabel.Text = UpgradeName;
        UpgradeButton.Pressed += () => EmitSignal(SignalName.UpgradeSelected, this);
        AddToGroup("Upgrade");
    }

    public void IncrementPurchaseCount()
    {
        PurchaseCount++;
        if (PurchaseCount >= MaxPurchases)
        {
            UpgradeButton.Disabled = true;
            UpgradeButton.Text = "MAX";
        }
    }

}