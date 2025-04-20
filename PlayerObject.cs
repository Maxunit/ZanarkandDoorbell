using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Lumina.Excel.Sheets;

namespace Doorbell;

public class PlayerObject {
    public uint LastSeen = 0;
    public string Name;
    public uint World;
    public bool IsLala;
    public string WorldName => Plugin.DataManager.GetExcelSheet<World>()?.GetRow(World).Name.ToString() ?? $"World_{World}";
    
    public Dictionary<int, string> SpeciesName = new Dictionary<int, string> {
        // {1, "Midlander Hyur"},
        // {2, "Highlander Hyur"},
        // {3, "Wildwood Elezen"},
        // {4, "Duskwight Elezen"},
        {5, "Lalafel"},
        {6, "Lalafel"},
        // {7, "Miqote"},
        // {12, "Aura"},
        // {15, "Viera"}
    };

    
    
    public PlayerObject(IPlayerCharacter character) {
        Name = character.Name.TextValue;
        World = character.HomeWorld.RowId;

        // Extract race ID from Customize array (index 4)
        
        var raceId = character.Customize[4];
        IsLala = SpeciesName.ContainsKey(raceId);
    }
}
