using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<string> LobbyMusicSelectedTrack =  
        CVarDef.Create("ambience.lobby_music_selected_track", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);
}