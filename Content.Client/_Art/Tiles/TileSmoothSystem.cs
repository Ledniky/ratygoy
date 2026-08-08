using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Art.Tiles;

[UsedImplicitly]
public sealed class TileSmoothSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    private TileSmoothOverlay _overlay = default!;

    private readonly Queue<(EntityUid GridUid, Vector2i Pos)> _dirtyTiles = new();

    private readonly HashSet<(EntityUid, Vector2i)> _queuedThisFrame = new();

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new TileSmoothOverlay();
        _overlayMan.AddOverlay(_overlay);

        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridInitializeEvent>(OnGridInit);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);

        _protoMan.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        while (_dirtyTiles.TryDequeue(out var entry))
        {
            _queuedThisFrame.Remove(entry);

            if (!TryComp<MapGridComponent>(entry.GridUid, out var grid))
                continue;

            _overlay.RecalculateTile(entry.GridUid, grid, entry.Pos);
        }
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _overlay.InvalidateAll();

        var query = AllEntityQuery<MapGridComponent>();
        while (query.MoveNext(out var uid, out var grid))
            EnqueueAllTiles(uid, grid);
    }

    private void EnqueueAllTiles(EntityUid gridUid, MapGridComponent grid)
    {
        foreach (var tileRef in _mapSystem.GetAllTiles(gridUid, grid))
            EnqueueTile(gridUid, tileRef.GridIndices);
    }

    private void OnGridRemoved(GridRemovalEvent ev)
    {
        _overlay.InvalidateGrid(ev.EntityUid);
    }

    private void OnGridInit(GridInitializeEvent ev)
    {
        _overlay.InvalidateGrid(ev.EntityUid);

        if (TryComp<MapGridComponent>(ev.EntityUid, out var grid))
            EnqueueAllTiles(ev.EntityUid, grid);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _protoMan.PrototypesReloaded -= OnPrototypesReloaded;
        _overlayMan.RemoveOverlay<TileSmoothOverlay>();
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        foreach (var change in ev.Changes)
        {
            var pos = change.GridIndices;

            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                EnqueueTile(ev.Entity.Owner, pos + new Vector2i(dx, dy));
        }
    }

    private void EnqueueTile(EntityUid gridUid, Vector2i pos)
    {
        var key = (gridUid, pos);
        if (_queuedThisFrame.Add(key))
            _dirtyTiles.Enqueue(key);
    }
}