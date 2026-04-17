using Godot;
using System;

[GlobalClass]
public partial class UpgradeInfo : Node
{
    [Export] public string UpgradeName;
    [Export] public int Cost;
    [Export] Label NameLabel;
    [Export] Button UpgradeButton;
    [Export] public string UpgradeDescription;

    [Signal] public delegate void UpgradeSelectedEventHandler(UpgradeInfo upgrade);

    public override void _Ready() {
        UpgradeButton.Text = "XP: " + Cost.ToString();
        NameLabel.Text = UpgradeName;
        UpgradeButton.Pressed += () => EmitSignal(SignalName.UpgradeSelected, this);
        AddToGroup("Upgrade");
    }


}