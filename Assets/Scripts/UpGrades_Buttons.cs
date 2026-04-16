using Godot;
using System;

public partial class UpGrades_Buttons : Control
{
	private Control slidePanel;
	private Control ultimatePanel;
	private Control snowPanel;
	private Control basePanel;

	public override void _Ready()
	{
		// Get panel references
		slidePanel = GetNode<Control>("BG-Slide-Upgrades");
		ultimatePanel = GetNode<Control>("BG-ULTIMATE-Upgrades");
		snowPanel = GetNode<Control>("BG-Snow-Upgrades");
		basePanel = GetNode<Control>("BG-Base-Upgrades");

		// Hide all at start
		HideAllPanels();
	}

	private void HideAllPanels()
	{
		slidePanel.Visible = false;
		ultimatePanel.Visible = false;
		snowPanel.Visible = false;
		basePanel.Visible = false;
	}

	// BUTTON FUNCTIONS ↓↓↓

	public void OnSlidePressed()
	{
		HideAllPanels();
		slidePanel.Visible = true;
	}

	public void OnUltimatePressed()
	{
		HideAllPanels();
		ultimatePanel.Visible = true;
	}

	public void OnSnowPressed()
	{
		HideAllPanels();
		snowPanel.Visible = true;
	}

	public void OnBasePressed()
	{
		HideAllPanels();
		basePanel.Visible = true;
	}

	// EXIT BUTTON (inside each panel)
	public void OnExitPressed()
	{
		this.Visible = false; // hides the whole UpGrades UI
	}
}
