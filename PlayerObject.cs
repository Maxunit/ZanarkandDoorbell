using Dalamud.Game.ClientState.Objects.SubKinds;
using Lumina.Excel.Sheets;

namespace Doorbell;

public class PlayerObject {
    public uint LastSeen = 0;
    public string Name;
    public uint World;
    public string WorldName => Plugin.DataManager.GetExcelSheet<World>()?.GetRow(World).Name.ToString() ?? $"World_{World}";

    public PlayerObject(IPlayerCharacter character) {
        Name = character.Name.TextValue;
        World = character.HomeWorld.RowId;
    }
}
