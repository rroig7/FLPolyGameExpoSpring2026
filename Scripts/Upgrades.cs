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

			if(upgrade.PurchaseCount >= upgrade.MaxPurchases)
			{
				RpcId(id, MethodName.PurchaseComplete, false, "", 0f, -1f, "");
				GD.Print($"Player {id} failed to purchase {upgrade.UpgradeName} (max purchases reached)");
				return;
			}

			if(upgrade.UpgradeDescription.ToLower().Contains("health") && Player.PlayerBase._currentHp == Player.PlayerBase.MaxHp)
			{
				//Refund if trying to upgrade health at max
				RpcId(id, MethodName.PurchaseComplete, false, "", 0f, -1f, "");
				GD.Print($"Player {id} failed to purchase {upgrade.UpgradeName} (health already at max)");
				return;
			}

			//Modify player to apply upgrade
			if(Player.XP >= upgrade.Cost)
			{
				Player.XP -= upgrade.Cost;
				upgrade.IncrementPurchaseCount();
				ApplyUpgrade(upgrade.UpgradeDescription, upgrade.modifier, upgrade.minValue);

				RpcId(id, MethodName.PurchaseComplete, true, upgrade.UpgradeDescription, upgrade.modifier, upgrade.minValue, path.ToString());
				GD.Print($"Player {id} purchased {upgrade.UpgradeName}");
			}
			else
			{
				RpcId(id, MethodName.PurchaseComplete, false, "", 0f, -1f, "");
				GD.Print($"Player {id} failed to purchase {upgrade.UpgradeName}");
			}
		}
	}

	//Server -> Client
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	void PurchaseComplete(bool success, string description, float modifier, float min, string upgradePath)
	{
		processingPurchase = false;
		RefreshXpLabel();
		// Skip on host: server already applied in ApplyPurchase and shares the same node
		if (success && !string.IsNullOrEmpty(description) && !GenericCore.Instance.IsServer)
		{
			ApplyUpgrade(description, modifier, min);
			if (!string.IsNullOrEmpty(upgradePath))
				GetNode<UpgradeInfo>(upgradePath)?.IncrementPurchaseCount();
		}
	}

	public void RefreshXpLabel()
	{
		if (XPLabel != null) XPLabel.Text = "XP: " + Player.XP.ToString();
	}


	void ApplyUpgrade(string description, float modifier, float min = -1)
	{
		var parts = description.Split("/");
		var target = parts[0];

		if(target.ToLower() == "player")
		{
			var ability = parts[1];
			var property = parts[2];
	
			switch(ability.ToLower())
			{
				case "dash":
					ModDash(property, modifier, min);
					break;
				
				case "shoot":
					ModShoot(property, modifier, min);
					break;
				
				case "ult":
					ModUlt(property, modifier, min);
					break;
				
				case "base":
					ModBase(property, modifier);
					break;
			}
		}
	}

	void ModDash(string property, float modifier, float min)
	{
		switch(property.ToLower())
		{	
			case "speed":
				Player.dashSpeed += modifier;
				break;

			case "duration":
				Player.dashDuration += modifier;
				break;
			
			case "cooldown":
				Player.dashCooldown = Mathf.Lerp(Player.dashCooldown, min, modifier);
				GD.PushWarning($"New dash cooldown: {Player.dashCooldown}");
				break;
		}
	}

	void ModUlt(string property, float modifier, float min)
	{
		switch(property.ToLower())
		{
			case "cooldown":
				Player.UltimateCooldown = Mathf.Lerp(Player.UltimateCooldown, min, modifier);
				GD.PushWarning($"New ultimate cooldown: {Player.UltimateCooldown}");
				break;
			
			case "radius":
				Player.UltimateRadius += modifier;
				Player.UltimateModel.Scale *= 1.1f;
				break;
			
			case "damage":
				Player.UltimateDamage += modifier;
				break;
		}
	}

	void ModShoot(string property, float modifier, float min)
	{
		switch(property.ToLower())
		{
			case "cooldown":
				Player.FireRate = Mathf.Lerp(Player.FireRate, min, modifier);
				GD.PushWarning($"New fire rate: {Player.FireRate}");
				break;
			
			case "damage":
				Player.BulletDamage += modifier;
				SyncTurretDamage();
				break;
		}
	}

	void SyncTurretDamage()
	{
		if (!GenericCore.Instance.IsServer) return;
		foreach (Node node in GetTree().GetNodesInGroup("turrets"))
		{
			if (node is Turret turret && turret.OwnerPeerId == (int)Player.MyId.OwnerId)
				turret.BulletDamage = Player.BulletDamage;
		}
	}

	void ModBase(string property, float modifier)
	{
		switch(property.ToLower())
		{
			case "health":
				Player.PlayerBase._currentHp = (int)Mathf.Clamp(Player.PlayerBase._currentHp + modifier, 0, Player.PlayerBase.MaxHp);
				break;
			
			case "turret":
				if (!Player.PlayerBase.isLeftTurretSpawned || !Player.PlayerBase.isRightTurretSpawned)
				{
					Player.PlayerBase.SpawnTurret();
				}
				break;
		}
	}
	
	
}
