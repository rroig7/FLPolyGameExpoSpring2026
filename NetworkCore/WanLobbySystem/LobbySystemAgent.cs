using Godot;
using System;
using System.Text;

public partial class LobbySystemAgent : Control
{
    public LobbyStreamlined myLobbySystem;

    [Export]
    public MultiplayerSynchronizer myId;

    [Export]
    public bool IsGameServer;

    [Export]
    public int numPlayers;

    [Export]
    public string gameName = "Default";

    [Export]
    public int gamePort;

    [Export]
    public Button GameButton;

    public override void _Ready()
    {
        GD.Print("Agent Created!");
        base._Ready();
        SlowStart();
    }

    public async void SlowStart()
    {
        if (IsMultiplayerAuthority())
        {
            string[] args = OS.GetCmdlineArgs();
            bool GoingToBeServer = false;    
            foreach (string arg in args)
            {
                if (arg == "GAMESERVER")
                {
                    GD.Print("This is a server!");
                    GoingToBeServer = true;
                }
            }
            await ToSignal(GetTree().CreateTimer(.1f), SceneTreeTimer.SignalName.Timeout);
            while (GoingToBeServer && GenericCore.Instance.IsServer == false)
            {
                await ToSignal(GetTree().CreateTimer(.1f), SceneTreeTimer.SignalName.Timeout);
            }
            
            gameName = LobbyStreamlined.Instance.tempGameName;
            
            //This will happen automatically if it is a server.
            GD.Print("Is this a game server: " + GenericCore.Instance.IsServer);
            if (GenericCore.Instance.IsServer)
            {
                GD.Print("Setting the button to visible!");
                //GameButton.Hide();
                //   RpcId(1, "SetGameServer", false);
                IsGameServer = true;
            }
            else
            {
                //RpcId(1, "SetGameServer", true);
                IsGameServer = false;
            }
            GameButton.Visible = IsGameServer;
            gamePort = GenericCore.Instance.GetPort();
            numPlayers = GenericCore.Instance._peers.Count;
        }
    }
    

    public override void _Process(double delta)
    {
        base._Process(delta);
        GameButton.Text = gameName +" ("+numPlayers+")";
        if (IsMultiplayerAuthority())
        {
            numPlayers = GenericCore.Instance._peers.Count-1;
        }
    }

    public void Click()
    {
        if(GenericCore.Instance.IsGenericCoreConnected == false)
        {
            GenericCore.Instance.SetPort(gamePort.ToString());
            GenericCore.Instance.SetIP(LobbyStreamlined.Instance.LobbyServerIP.ToString());
            GenericCore.Instance.JoinGame();

        }
    }



}
