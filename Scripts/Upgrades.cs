using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Upgrades : Control
{
	[Export] Player Player;
	UpgradeInfo[] AllUpgrades;
	[Export] Label XPLabel;
	bool processingPurchase = false;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		//Ensures we wait long enough for all upgrades to be initalized before we collect them all
		Player.MyId.NetIDReady += SlowStart;
	}

	void SlowStart()
	{

		if(!Player.MyId.IsLocal) return;
		AllUpgrades = [.. GetTree().GetNodesInGroup("Upgrade").Cast<UpgradeInfo>()];

		GD.PushWarning($"Found {AllUpgrades.Length} upgrades in scene");

		foreach(var upgrade in AllUpgrades)
		{
			upgrade.UpgradeSelected += Purchase;
		}
	}


	public void Purchase(UpgradeInfo upgrade)
	{
		if(processingPurchase) return;
		processingPurchase = true;

		RpcId(1, MethodName.ApplyPurchase, Player.MyId.OwnerId, upgrade.GetPath());
	}


	//Client -> Server
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void ApplyPurchase(int id, NodePath path)
	{
		if(GenericCore.Instance.IsServer)
		{
			var upgrade = GetNode<UpgradeInfo>(path);
			//Modify player to apply upgrade

			if(Player.XP >= upgrade.Cost)
			{
				Player.XP -= upgrade.Cost;
				ApplyUpgrade(upgrade.UpgradeDescription);

				RpcId(id, MethodName.PurchaseComplete, true);
				GD.Print($"Player {id} purchased {upgrade.UpgradeName}");
			}
			else
			{
				RpcId(id, MethodName.PurchaseComplete, false);
				GD.Print($"Player {id} failed to purchase {upgrade.UpgradeName}");
			}
		}
	}

	//Server -> Client
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void PurchaseComplete(bool success)
	{
		processingPurchase = false;
		XPLabel.Text = "XP: " + Player.XP.ToString();
	}


	void ApplyUpgrade(string description)
	{
		var parts = description.Split("/");
		var target = parts[0];

		if(target.ToLower() == "player")
		{
			var ability = parts[1];
			var modifier = float.Parse(parts[2]);

			switch(ability.ToLower())
			{
				case "dash":
					Player.dashDuration += modifier;
					break;
				
				case "shoot":
					break;
			}
		}
	}
}
