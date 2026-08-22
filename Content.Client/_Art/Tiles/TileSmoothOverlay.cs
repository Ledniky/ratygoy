using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;
using System.Linq;

namespace Content.Client._Art.Tiles;

public sealed class TileSmoothOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _xformSys;

    private List<Entity<MapGridComponent>> _grids = new();

    private readonly Dictionary<string, RSI> _rsiCache = new();

    private readonly Dictionary<(EntityUid, Vector2i), Corners> _cornerCache = new();

    [Flags]
    private enum CornerFill : byte
    {
        None             = 0,
        CounterClockwise = 1,
        Diagonal         = 2,
        Clockwise        = 4,
    }

    private readonly record struct Corners(
        CornerFill NE, CornerFill NW, CornerFill SW, CornerFill SE);

    public TileSmoothOverlay()
    {
        IoCManager.InjectDependencies(this);
        _mapSystem = _entManager.System<SharedMapSystem>();
        _xformSys  = _entManager.System<SharedTransformSystem>();
    }

    public void InvalidateGrid(EntityUid gridUid)
    {
        var keys = _cornerCache.Keys.Where(k => k.Item1 == gridUid).ToList();
        foreach (var key in keys)
            _cornerCache.Remove(key);
    }

    public void InvalidateAll()
    {
        _cornerCache.Clear();
        _rsiCache.Clear();
    }

    public void InvalidateTile(EntityUid gridUid, Vector2i pos)
    {
        _cornerCache.Remove((gridUid, pos));
    }

    public void RecalculateTile(EntityUid gridUid, MapGridComponent grid, Vector2i pos)
    {
        if (!_mapSystem.TryGetTileRef(gridUid, grid, pos, out var tileRef) || tileRef.Tile.IsEmpty)
        {
            _cornerCache.Remove((gridUid, pos));
            return;
        }

        var tileDef = _tileDefManager[tileRef.Tile.TypeId] as ContentTileDefinition;
        if (tileDef?.SmoothKey == null)
        {
            _cornerCache.Remove((gridUid, pos));
            return;
        }

        _cornerCache[(gridUid, pos)] =
            CalculateCornerFill(gridUid, grid, pos, tileDef.SmoothKey, tileDef.SmoothConnectAll);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var drawHandle = args.WorldHandle;

        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        foreach (var grid in _grids)
        {
            var worldMatrix = _xformSys.GetWorldMatrix(grid.Owner);
            drawHandle.SetTransform(worldMatrix);

            var tileEnumerator = _mapSystem.GetTilesEnumerator(grid.Owner, grid.Comp, args.WorldBounds);

            while (tileEnumerator.MoveNext(out var tileRef))
            {
                if (tileRef.Tile.IsEmpty)
                    continue;

                var tileDef = _tileDefManager[tileRef.Tile.TypeId] as ContentTileDefinition;
                if (tileDef?.SmoothKey == null || tileDef.SmoothBase == null || tileDef.SmoothRsi == null)
                    continue;

                var rsi = GetRsi(tileDef.SmoothRsi.Value.ToString());
                if (rsi == null)
                    continue;

                var pos = tileRef.GridIndices;

                if (!_cornerCache.TryGetValue((grid.Owner, pos), out var corners))
                {
                    corners = CalculateCornerFill(grid.Owner, grid.Comp, pos, tileDef.SmoothKey, tileDef.SmoothConnectAll);
                    _cornerCache[(grid.Owner, pos)] = corners;
                }

                DrawCorner(drawHandle, rsi, tileDef.SmoothBase, pos, corners.SE, RsiDirection.South);
                DrawCorner(drawHandle, rsi, tileDef.SmoothBase, pos, corners.NE, RsiDirection.East);
                DrawCorner(drawHandle, rsi, tileDef.SmoothBase, pos, corners.NW, RsiDirection.North);
                DrawCorner(drawHandle, rsi, tileDef.SmoothBase, pos, corners.SW, RsiDirection.West);
            }
        }

        drawHandle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawCorner(
        DrawingHandleWorld handle, RSI rsi, string stateBase,
        Vector2i tilePos, CornerFill fill, RsiDirection direction)
    {
        var stateName = $"{stateBase}{(int)fill}";
        if (!rsi.TryGetState(stateName, out var state))
            return;

        var frames = state.GetFrames(direction);
        if (frames.Length == 0)
            return;

        handle.DrawTexture(frames[0], tilePos);
    }

    private Corners CalculateCornerFill(EntityUid gridUid, MapGridComponent grid, Vector2i pos, string smoothKey, bool connectAll)
    {
        var n  = IsMatchingTile(gridUid, grid, pos.Offset(Direction.North),     smoothKey, connectAll);
        var ne = IsMatchingTile(gridUid, grid, pos.Offset(Direction.NorthEast), smoothKey, connectAll);
        var e  = IsMatchingTile(gridUid, grid, pos.Offset(Direction.East),      smoothKey, connectAll);
        var se = IsMatchingTile(gridUid, grid, pos.Offset(Direction.SouthEast), smoothKey, connectAll);
        var s  = IsMatchingTile(gridUid, grid, pos.Offset(Direction.South),     smoothKey, connectAll);
        var sw = IsMatchingTile(gridUid, grid, pos.Offset(Direction.SouthWest), smoothKey, connectAll);
        var w  = IsMatchingTile(gridUid, grid, pos.Offset(Direction.West),      smoothKey, connectAll);
        var nw = IsMatchingTile(gridUid, grid, pos.Offset(Direction.NorthWest), smoothKey, connectAll);

        var cornerNE = CornerFill.None;
        var cornerSE = CornerFill.None;
        var cornerSW = CornerFill.None;
        var cornerNW = CornerFill.None;

        if (n)  { cornerNE |= CornerFill.CounterClockwise; cornerNW |= CornerFill.Clockwise; }
        if (ne) { cornerNE |= CornerFill.Diagonal; }
        if (e)  { cornerNE |= CornerFill.Clockwise; cornerSE |= CornerFill.CounterClockwise; }
        if (se) { cornerSE |= CornerFill.Diagonal; }
        if (s)  { cornerSE |= CornerFill.Clockwise; cornerSW |= CornerFill.CounterClockwise; }
        if (sw) { cornerSW |= CornerFill.Diagonal; }
        if (w)  { cornerSW |= CornerFill.Clockwise; cornerNW |= CornerFill.CounterClockwise; }
        if (nw) { cornerNW |= CornerFill.Diagonal; }

        return new Corners(cornerNE, cornerNW, cornerSW, cornerSE);
    }

    private bool IsMatchingTile(EntityUid gridUid, MapGridComponent grid, Vector2i pos, string smoothKey, bool connectAll)
    {
        if (!_mapSystem.TryGetTileRef(gridUid, grid, pos, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        if (connectAll)
            return true;

        var def = _tileDefManager[tileRef.Tile.TypeId] as ContentTileDefinition;
        return def?.SmoothKey == smoothKey;
    }

    private RSI? GetRsi(string path)
    {
        if (_rsiCache.TryGetValue(path, out var cached))
            return cached;

        try
        {
            var rsi = _resourceCache.GetResource<RSIResource>(path).RSI;
            _rsiCache[path] = rsi;
            return rsi;
        }
        catch
        {
            return null;
        }
    }
}