using Content.Server.Power.EntitySystems;
using Content.Shared.Research.Components;
using System.Linq;
using Content.Shared.IdentityManagement; // Art-edit

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    private void InitializeServer()
    {
        SubscribeLocalEvent<ResearchServerComponent, ComponentStartup>(OnServerStartup);
        SubscribeLocalEvent<ResearchServerComponent, ComponentShutdown>(OnServerShutdown);
        SubscribeLocalEvent<ResearchServerComponent, TechnologyDatabaseModifiedEvent>(OnServerDatabaseModified);
        // Art-start
        SubscribeLocalEvent<ResearchServerComponent, BoundUIOpenedEvent>(OnServerBuiOpened);
        SubscribeLocalEvent<ResearchServerComponent, ToggleResearchClientMessage>(OnToggleResearchClient);
        // Art-end
    }

    private void OnServerStartup(EntityUid uid, ResearchServerComponent component, ComponentStartup args)
    {
        var unusedId = EntityQuery<ResearchServerComponent>(true)
            .Max(s => s.Id) + 1;
        component.Id = unusedId;
        Dirty(uid, component);
    }

    private void OnServerShutdown(EntityUid uid, ResearchServerComponent component, ComponentShutdown args)
    {
        foreach (var client in new List<EntityUid>(component.Clients))
        {
            UnregisterClient(client, uid, serverComponent: component, dirtyServer: false);
        }
    }

    private void OnServerDatabaseModified(EntityUid uid, ResearchServerComponent component, ref TechnologyDatabaseModifiedEvent args)
    {
        foreach (var client in component.Clients)
        {
            RaiseLocalEvent(client, ref args);
        }
    }

    private bool CanRun(EntityUid uid)
    {
        return this.IsPowered(uid, EntityManager);
    }

    // Art-start
    private void OnServerBuiOpened(EntityUid uid, ResearchServerComponent component, BoundUIOpenedEvent args)
    {
        UpdateServerUi((uid, component));
    }

    private void OnToggleResearchClient(EntityUid uid, ResearchServerComponent component, ToggleResearchClientMessage args)
    {
        var client = GetEntity(args.Client);

        if (!HasComp<ResearchClientComponent>(client))
            return;

        if (component.AllowedClients.Contains(client))
        {
            component.AllowedClients.Remove(client);
            UnregisterClient(client, uid, serverComponent: component);
        }
        else
        {
            component.AllowedClients.Add(client);
        }

        Dirty(uid, component);
        UpdateServerUi((uid, component));
        UpdateClientInterface(client);
    }

    private void UpdateServerUi(Entity<ResearchServerComponent> ent)
    {
        if (!_uiSystem.IsUiOpen(ent.Owner, ResearchServerUiKey.Key))
            return;

        var serverXform = Transform(ent.Owner);
        if (serverXform.GridUid is not { } grid)
            return;

        var clientSet = new HashSet<Entity<ResearchClientComponent>>();
        _lookup.GetGridEntities(grid, clientSet);

        var clientList = new List<(NetEntity, string, string)>();

        foreach (var client in clientSet)
        {
            var worldPos = _xformSystem.GetWorldPosition(client.Owner);
            var posStr = $"({(int)MathF.Round(worldPos.X)}, {(int)MathF.Round(worldPos.Y)})";

            var name = Identity.Name(client.Owner, EntityManager);
            var allowed = ent.Comp.AllowedClients.Contains(client.Owner);
            var connected = ent.Comp.Clients.Contains(client.Owner);

            var displayText = Loc.GetString("research-server-ui-client-entry",
                ("name", name),
                ("pos", posStr),
                ("allowed", allowed),
                ("connected", connected));

            clientList.Add((GetNetEntity(client.Owner), displayText, name));
        }

        _uiSystem.SetUiState(ent.Owner, ResearchServerUiKey.Key, new ResearchServerBuiState(clientList));
    }
    // Art-end

    private void UpdateServer(EntityUid uid, int time, ResearchServerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!CanRun(uid))
            return;
        ModifyServerPoints(uid, GetPointsPerSecond(uid, component) * time, component);
    }

    /// <summary>
    /// Registers a client to the specified server.
    /// </summary>
    /// <param name="client">The client being registered</param>
    /// <param name="server">The server the client is being registered to</param>
    /// <param name="clientComponent"></param>
    /// <param name="serverComponent"></param>
    /// <param name="dirtyServer">Whether or not to dirty the server component after registration</param>
    public void RegisterClient(EntityUid client, EntityUid server, ResearchClientComponent? clientComponent = null,
        ResearchServerComponent? serverComponent = null, bool dirtyServer = true)
    {
        if (!Resolve(client, ref clientComponent, false) || !Resolve(server, ref serverComponent, false))
            return;

        if (serverComponent.Clients.Contains(client))
            return;

        serverComponent.Clients.Add(client);
        clientComponent.Server = server;
        SyncClientWithServer(client, clientComponent: clientComponent);

        if (dirtyServer && !TerminatingOrDeleted(server))
            Dirty(server, serverComponent);

        var ev = new ResearchRegistrationChangedEvent(server);
        RaiseLocalEvent(client, ref ev);
    }

    /// <summary>
    /// Unregisterse a client from its server
    /// </summary>
    /// <param name="client"></param>
    /// <param name="clientComponent"></param>
    /// <param name="dirtyServer"></param>
    public void UnregisterClient(EntityUid client, ResearchClientComponent? clientComponent = null, bool dirtyServer = true)
    {
        if (!Resolve(client, ref clientComponent))
            return;

        if (clientComponent.Server is not { } server)
            return;

        UnregisterClient(client, server, clientComponent, dirtyServer: dirtyServer);
    }

    /// <summary>
    /// Unregisters a client from its server
    /// </summary>
    /// <param name="client"></param>
    /// <param name="server"></param>
    /// <param name="clientComponent"></param>
    /// <param name="serverComponent"></param>
    /// <param name="dirtyServer"></param>
    public void UnregisterClient(EntityUid client, EntityUid server, ResearchClientComponent? clientComponent = null,
        ResearchServerComponent? serverComponent = null, bool dirtyServer = true)
    {
        if (!Resolve(client, ref clientComponent, false) || !Resolve(server, ref serverComponent, false))
            return;

        serverComponent.Clients.Remove(client);
        clientComponent.Server = null;
        SyncClientWithServer(client, clientComponent: clientComponent);

        if (dirtyServer && !TerminatingOrDeleted(server))
        {
            Dirty(server, serverComponent);
        }

        var ev = new ResearchRegistrationChangedEvent(null);
        RaiseLocalEvent(client, ref ev);
    }

    /// <summary>
    /// Gets the amount of points generated by all the server's sources in a second.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    public int GetPointsPerSecond(EntityUid uid, ResearchServerComponent? component = null)
    {
        var points = 0;

        if (!Resolve(uid, ref component))
            return points;

        if (!CanRun(uid))
            return points;

        var ev = new ResearchServerGetPointsPerSecondEvent(uid, points);
        foreach (var client in component.Clients)
        {
            RaiseLocalEvent(client, ref ev);
        }
        return ev.Points;
    }

    /// <summary>
    /// Adds a specified number of points to a server.
    /// </summary>
    /// <param name="uid">The server</param>
    /// <param name="points">The amount of points being added</param>
    /// <param name="component"></param>
    public void ModifyServerPoints(EntityUid uid, int points, ResearchServerComponent? component = null)
    {
        if (points == 0)
            return;

        if (!Resolve(uid, ref component))
            return;
        component.Points += points;
        var ev = new ResearchServerPointsChangedEvent(uid, component.Points, points);
        foreach (var client in component.Clients)
        {
            RaiseLocalEvent(client, ref ev);
        }
        Dirty(uid, component);
    }
}
