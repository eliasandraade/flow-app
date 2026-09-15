using Flow.Domain.Entities;
using Flow.Domain.Enums;
using Flow.Domain.ValueObjects;
using Flow.Infrastructure.Persistence.Mongo;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Flow.Infrastructure.Seeding;

/// <summary>
/// Builds the demonstration dataset: a coherent story of corporate innovation rather than
/// random rows, so the dashboard shows something worth looking at and every screen has
/// real content behind it.
///
/// Runs only when SEED_DEMO_DATA is enabled, and is idempotent — a second run finds the
/// marker guideline and does nothing.
/// </summary>
public sealed class DemoDataSeeder
{
    private readonly FlowMongoContext _context;
    private readonly UserManager<User> _userManager;
    private readonly DemoSeedOptions _options;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        FlowMongoContext context,
        UserManager<User> userManager,
        DemoSeedOptions options,
        ILogger<DemoDataSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _options = options;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var already = await _context.Guidelines
            .Find(Builders<StrategicGuideline>.Filter.Eq(g => g.Title, Narrative.MarkerGuidelineTitle))
            .AnyAsync(cancellationToken);

        if (already)
        {
            _logger.LogInformation("Demo data already present. Nothing to seed.");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var people = await SeedPeopleAsync();
        var guidelines = await SeedGuidelinesAsync(people.Leader, now, cancellationToken);
        var ideas = await SeedIdeasAsync(people, guidelines, now, cancellationToken);
        await SeedProjectsAsync(people, guidelines, ideas, now, cancellationToken);

        _logger.LogInformation("Demo dataset seeded.");
    }

    // ---------------------------------------------------------------------------
    // People
    // ---------------------------------------------------------------------------

    private async Task<DemoPeople> SeedPeopleAsync()
    {
        var operatorUser = await EnsureUserAsync("Carla Souza", _options.OperatorEmail, UserRole.Operator);
        var secondOperator = await EnsureUserAsync("Bruno Tavares", "bruno@flow.demo", UserRole.Operator);
        var manager = await EnsureUserAsync("Ana Ribeiro", _options.ManagerEmail, UserRole.Manager);
        var leader = await EnsureUserAsync("Marcos Leal", _options.LeadershipEmail, UserRole.Leadership);

        return new DemoPeople(operatorUser, secondOperator, manager, leader);
    }

    private async Task<User> EnsureUserAsync(string name, string email, UserRole role)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null) return existing;

        var user = User.Create(name, email, role);
        var result = await _userManager.CreateAsync(user, _options.Password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed demo user '{email}': "
                + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, role.ToString());
        _logger.LogInformation("Seeded demo user {Email} as {Role}.", email, role);

        return user;
    }

    // ---------------------------------------------------------------------------
    // Strategy
    // ---------------------------------------------------------------------------

    private async Task<DemoGuidelines> SeedGuidelinesAsync(
        User leader, DateTimeOffset now, CancellationToken ct)
    {
        var yearStart = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var rework = StrategicGuideline.Create(
            Narrative.MarkerGuidelineTitle,
            "Reduzir em 30% o retrabalho na linha de montagem até o fim do ciclo, atacando as causas registradas nas paradas não programadas.",
            GuidelineCategory.OperationalEfficiency,
            Narrative.OperationalCampaign,
            yearStart, yearStart.AddYears(1).AddDays(-1), leader.Id);

        var logistics = StrategicGuideline.Create(
            "Reduzir o custo logístico por entrega",
            "Baixar em 15% o custo médio por entrega sem perder nível de serviço, priorizando roteirização e ocupação de carga.",
            GuidelineCategory.CostReduction,
            Narrative.OperationalCampaign,
            yearStart, yearStart.AddYears(1).AddDays(-1), leader.Id);

        var inspection = StrategicGuideline.Create(
            "Digitalizar a inspeção de qualidade",
            "Substituir a inspeção em papel por captura digital com trilha de evidência, reduzindo tempo de laudo e perda de registro.",
            GuidelineCategory.DigitalTransformation,
            Narrative.DigitalCampaign,
            yearStart.AddMonths(2), null, leader.Id);

        var safety = StrategicGuideline.Create(
            "Elevar a segurança na expedição",
            "Zerar acidentes com empilhadeira na área de expedição por meio de barreiras físicas e alerta de proximidade.",
            GuidelineCategory.Safety,
            null,
            yearStart.AddMonths(1), null, leader.Id);

        // Deliberately expired, so the demo can show that validity is derived from the
        // period and that history survives the end of a strategy.
        var energy = StrategicGuideline.Create(
            "Programa de eficiência energética",
            "Ciclo anterior de eficiência energética, encerrado após atingir a meta de redução de consumo.",
            GuidelineCategory.Sustainability,
            "Ciclo Energia 2025",
            yearStart.AddYears(-1), yearStart.AddDays(-1), leader.Id);

        var all = new[] { rework, logistics, inspection, safety, energy };
        await _context.Guidelines.InsertManyAsync(all, cancellationToken: ct);

        var history = all
            .Select(g => StrategicGuidelineHistoryEntry.Capture(
                g, GuidelineChangeType.Created, leader.Id, leader.Name))
            .ToList();

        history.Add(StrategicGuidelineHistoryEntry.Capture(
            energy, GuidelineChangeType.Closed, leader.Id, leader.Name));

        await _context.GuidelineHistory.InsertManyAsync(history, cancellationToken: ct);

        await _context.AuditLogs.InsertManyAsync(
            all.Select(g => AuditLog.Create(
                nameof(StrategicGuideline), g.Id, "Created", leader.Id, leader.Name,
                newValue: g.Title)),
            cancellationToken: ct);

        return new DemoGuidelines(rework, logistics, inspection, safety, energy);
    }

    // ---------------------------------------------------------------------------
    // Ideas
    // ---------------------------------------------------------------------------

    private async Task<DemoIdeas> SeedIdeasAsync(
        DemoPeople people, DemoGuidelines guidelines, DateTimeOffset now, CancellationToken ct)
    {
        var ideas = new List<(Idea Idea, int AgeDays)>();
        var audits = new List<AuditLog>();
        var comments = new List<IdeaComment>();
        var notifications = new List<Notification>();

        // The one that made it all the way through to a measured result.
        var sensor = Idea.Create(
            "Sensor de vibração para manutenção preditiva",
            "Instalar sensores de vibração nos motores críticos da linha 3 e disparar ordem de manutenção antes da falha, usando o histórico de paradas como base do limiar.",
            "As paradas não programadas da linha 3 concentram 40% do retrabalho do mês e só são percebidas depois que a peça já saiu fora de especificação.",
            people.Operator.Id, people.Operator.Name, guidelines.Rework.Id);
        sensor.SetPriority(IdeaPriority.High);
        sensor.SetScore(88);
        sensor.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(9, 9, 6, 8, 8)));
        sensor.Submit();
        sensor.Approve("Alinhada com a diretriz de retrabalho e com evidência de parada registrada. Aprovada para piloto na linha 3.");
        ideas.Add((sensor, 150));

        // Approved and converted, still running.
        var routing = Idea.Create(
            "Roteirização dinâmica das entregas urbanas",
            "Recalcular a rota do dia a partir de janela de entrega, ocupação do veículo e trânsito, em vez de repetir a rota fixa da semana anterior.",
            "A rota é montada uma vez por semana e não acompanha cancelamento nem reagendamento, o que gera viagem com caminhão meio vazio.",
            people.SecondOperator.Id, people.SecondOperator.Name, guidelines.Logistics.Id);
        routing.SetPriority(IdeaPriority.High);
        routing.SetScore(81);
        routing.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(8, 8, 5, 7, 7)));
        routing.Submit();
        routing.Approve("Impacto direto no custo por entrega. Aprovada com escopo restrito à região metropolitana.");
        ideas.Add((routing, 120));

        // Approved and converted, currently blocked.
        var tablet = Idea.Create(
            "Laudo de inspeção em tablet com foto obrigatória",
            "Trocar a ficha de papel por um formulário em tablet que exige foto do ponto inspecionado e grava a evidência junto do laudo.",
            "O laudo em papel se perde entre a inspeção e o arquivo, e quando o cliente questiona não existe evidência do que foi inspecionado.",
            people.Operator.Id, people.Operator.Name, guidelines.Inspection.Id);
        tablet.SetPriority(IdeaPriority.Medium);
        tablet.SetScore(74);
        tablet.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(8, 7, 5, 6, 7)));
        tablet.Submit();
        tablet.Approve("Aprovada. Depende da compra dos tablets, então o cronograma tem risco de fornecedor.");
        ideas.Add((tablet, 100));

        // Approved and delivered.
        var barrier = Idea.Create(
            "Barreira física e alerta sonoro na expedição",
            "Separar fisicamente o corredor de pedestres da rota das empilhadeiras e instalar alerta sonoro de proximidade nos cruzamentos.",
            "Pedestres e empilhadeiras dividem o mesmo corredor na expedição, e já houve dois quase-acidentes registrados no trimestre.",
            people.SecondOperator.Id, people.SecondOperator.Name, guidelines.Safety.Id);
        barrier.SetPriority(IdeaPriority.High);
        barrier.SetScore(92);
        barrier.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(9, 8, 8, 9, 9)));
        barrier.Submit();
        barrier.Approve("Prioridade máxima por se tratar de segurança. Aprovada para execução imediata.");
        ideas.Add((barrier, 200));

        // Waiting in the manager queue, already scored — the review screen has content.
        var checklist = Idea.Create(
            "Checklist digital de troca de turno",
            "Padronizar a passagem de turno em um checklist digital com pendências abertas, para que o turno seguinte não descubra o problema pela metade.",
            "A passagem de turno é verbal e as pendências se perdem, o que faz o turno da noite refazer diagnóstico que o turno da tarde já tinha feito.",
            people.Operator.Id, people.Operator.Name, guidelines.Rework.Id);
        checklist.SetPriority(IdeaPriority.Medium);
        checklist.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(7, 6, 8, 5, 6)));
        checklist.Submit();
        ideas.Add((checklist, 25));

        var packaging = Idea.Create(
            "Reaproveitar embalagem de transporte interno",
            "Criar um circuito fechado de caixas retornáveis entre o almoxarifado e a linha, em vez de descartar embalagem a cada movimentação.",
            "Toda movimentação interna consome embalagem nova, que é descartada no mesmo dia.",
            people.SecondOperator.Id, people.SecondOperator.Name, guidelines.Logistics.Id);
        packaging.SetPriority(IdeaPriority.Low);
        packaging.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(6, 5, 7, 3, 6)));
        packaging.Submit();
        ideas.Add((packaging, 18));

        var qrcode = Idea.Create(
            "QR code de rastreio no palete",
            "Identificar cada palete com QR code para saber onde ele está sem precisar percorrer o galpão.",
            "Localizar um palete específico depende de conhecimento pessoal do conferente e leva de 15 a 40 minutos.",
            people.Operator.Id, people.Operator.Name, guidelines.Inspection.Id);
        qrcode.SetPriority(IdeaPriority.Medium);
        qrcode.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(7, 7, 6, 6, 5)));
        qrcode.Submit();
        ideas.Add((qrcode, 12));

        // Rejected, with a real reason — the trail has to show refusals too.
        var app = Idea.Create(
            "Aplicativo próprio de transporte para funcionários",
            "Desenvolver internamente um aplicativo de caronas entre funcionários com rastreamento em tempo real.",
            "Falta transporte no turno da madrugada e as pessoas dependem de combinação informal.",
            people.SecondOperator.Id, people.SecondOperator.Name, null);
        app.SetPriority(IdeaPriority.Low);
        app.SetFlowScore(FlowScore.Compute(new FlowScoreComponents(2, 4, 2, 5, 4)));
        app.Submit();
        app.Reject("Problema real e reconhecido, mas construir e manter um aplicativo de transporte está fora da nossa competência e não se conecta a nenhuma diretriz vigente. Encaminhado para RH avaliar fretamento.");
        ideas.Add((app, 60));

        // Drafts, so the operator journey opens on something editable.
        var lighting = Idea.Create(
            "Revisar a iluminação do setor de acabamento",
            "Trocar as luminárias do acabamento por LED com sensor de presença, melhorando a inspeção visual e reduzindo consumo.",
            "A iluminação atual dificulta enxergar defeito superficial e obriga a repetir a conferência sob a luz da bancada.",
            people.Operator.Id, people.Operator.Name, guidelines.Rework.Id);
        ideas.Add((lighting, 5));

        var training = Idea.Create(
            "Trilha rápida de treinamento para novos operadores",
            "Montar uma trilha curta em vídeo para as cinco operações que mais geram erro de novato.",
            "Operador novo aprende observando, e o erro só aparece na inspeção final.",
            people.SecondOperator.Id, people.SecondOperator.Name, null);
        ideas.Add((training, 2));

        await _context.Ideas.InsertManyAsync(ideas.Select(x => x.Idea), cancellationToken: ct);

        foreach (var (idea, ageDays) in ideas)
            await BackdateAsync(_context.Ideas, idea.Id, now.AddDays(-ageDays), ct);

        // Manager comments on the queue, so the review screen is not empty.
        comments.Add(IdeaComment.Create(checklist.Id, people.Manager.Id, people.Manager.Name,
            "Boa ideia. Antes de pontuar, preciso saber se o checklist substitui o registro em papel ou soma a ele."));
        comments.Add(IdeaComment.Create(qrcode.Id, people.Manager.Id, people.Manager.Name,
            "Faz sentido, mas quero comparar com a ideia do tablet antes de decidir: pode ser o mesmo projeto."));
        comments.Add(IdeaComment.Create(sensor.Id, people.Manager.Id, people.Manager.Name,
            "Puxei o histórico de paradas da linha 3 e a evidência confere. Seguindo para projeto."));

        await _context.IdeaComments.InsertManyAsync(comments, cancellationToken: ct);

        // Points for the two approved authors, with the ledger entries that justify them.
        var ledger = new List<PointLedgerEntry>
        {
            PointLedgerEntry.Create(people.Operator.Id, 50, "Idea approved", nameof(Idea), sensor.Id),
            PointLedgerEntry.Create(people.Operator.Id, 50, "Idea approved", nameof(Idea), tablet.Id),
            PointLedgerEntry.Create(people.SecondOperator.Id, 50, "Idea approved", nameof(Idea), routing.Id),
            PointLedgerEntry.Create(people.SecondOperator.Id, 50, "Idea approved", nameof(Idea), barrier.Id)
        };

        await _context.PointLedger.InsertManyAsync(ledger, cancellationToken: ct);
        await IncrementPointsAsync(people.Operator.Id, 100, ct);
        await IncrementPointsAsync(people.SecondOperator.Id, 100, ct);

        foreach (var (idea, _) in ideas)
        {
            audits.Add(AuditLog.Create(nameof(Idea), idea.Id, "Created",
                idea.SubmittedBy, idea.SubmittedByName, newValue: nameof(IdeaStatus.Draft)));

            if (idea.Status == IdeaStatus.Draft) continue;

            audits.Add(AuditLog.Create(nameof(Idea), idea.Id, "Submitted",
                idea.SubmittedBy, idea.SubmittedByName,
                oldValue: nameof(IdeaStatus.Draft), newValue: nameof(IdeaStatus.UnderReview)));

            if (idea.Status is IdeaStatus.Approved or IdeaStatus.Rejected)
            {
                audits.Add(AuditLog.Create(nameof(Idea), idea.Id, idea.Status.ToString(),
                    people.Manager.Id, people.Manager.Name,
                    oldValue: nameof(IdeaStatus.UnderReview), newValue: idea.Status.ToString(),
                    reason: idea.ManagerComment));

                notifications.Add(Notification.Create(
                    idea.SubmittedBy,
                    idea.Status == IdeaStatus.Approved
                        ? NotificationType.IdeaApproved
                        : NotificationType.IdeaRejected,
                    idea.Status == IdeaStatus.Approved ? "Sua ideia foi aprovada" : "Sua ideia não foi aprovada",
                    $"\"{idea.Title}\"",
                    $"flow://ideas/{idea.Id}"));
            }
        }

        notifications.Add(Notification.Create(
            people.Manager.Id, NotificationType.IdeaAwaitingReview,
            "3 ideias aguardando análise",
            "A fila de análise tem ideias pontuadas e prontas para decisão.",
            "flow://manager/ideas"));

        await _context.AuditLogs.InsertManyAsync(audits, cancellationToken: ct);
        await _context.Notifications.InsertManyAsync(notifications, cancellationToken: ct);

        return new DemoIdeas(sensor, routing, tablet, barrier);
    }

    // ---------------------------------------------------------------------------
    // Projects and results
    // ---------------------------------------------------------------------------

    private async Task SeedProjectsAsync(
        DemoPeople people, DemoGuidelines guidelines, DemoIdeas ideas,
        DateTimeOffset now, CancellationToken ct)
    {
        var projects = new List<Project>();
        var snapshots = new List<ProjectSnapshot>();
        var audits = new List<AuditLog>();
        var results = new List<Result>();
        var notifications = new List<Notification>();
        var timelines = new List<ProjectTimeline>();

        var actor = people.Manager;

        // 1. Delivered with measured results — the story that closes the loop.
        var safetyProject = Project.Create(
            "Corredor seguro na expedição",
            "Instalação de barreiras físicas, sinalização de solo e alerta de proximidade nos três cruzamentos da expedição.",
            people.Manager.Id, people.Manager.Name, ProjectPriority.Critical,
            sourceIdeaId: ideas.Barrier.Id,
            linkedGuidelineId: guidelines.Safety.Id,
            estimatedCost: 85_000m,
            deadline: now.AddDays(-20));
        Track(safetyProject, "Created", null);
        safetyProject.Start();
        Track(safetyProject, "Started", nameof(ProjectStatus.Planned));
        safetyProject.AdvanceStage(ProjectStage.Execution);
        Track(safetyProject, "StageChanged", nameof(ProjectStage.Planning));
        safetyProject.UpdateProgress(60);
        Track(safetyProject, "ProgressUpdated", "0");
        safetyProject.Complete();
        Track(safetyProject, "Completed", nameof(ProjectStatus.InProgress));
        timelines.Add(new ProjectTimeline(safetyProject.Id,
            CreatedAt: now.AddDays(-190), StartedAt: now.AddDays(-182),
            EndedAt: now.AddDays(-118), BlockedSince: null));

        var safetyResult = Result.Create(safetyProject.Id, actor.Id);
        safetyResult.SetEstimated(0m, 120_000m, 85_000m);
        safetyResult.SetActual(0m, 143_000m, 79_400m);
        safetyResult.SetImpactMetrics(4.5m, 180m, 22m);
        safetyResult.SetNotes(7, "Zero acidentes registrados desde a entrega. Economia calculada sobre afastamento evitado e parada de área.");
        results.Add(safetyResult);

        // 2. Delivered, financially modest but strong on non-financial impact.
        var reworkProject = Project.Create(
            "Piloto de manutenção preditiva na linha 3",
            "Sensores de vibração nos seis motores críticos da linha 3, com limiar calibrado sobre o histórico de paradas e ordem de manutenção automática.",
            people.Manager.Id, people.Manager.Name, ProjectPriority.High,
            sourceIdeaId: ideas.Sensor.Id,
            linkedGuidelineId: guidelines.Rework.Id,
            estimatedCost: 140_000m,
            deadline: now.AddDays(-5));
        Track(reworkProject, "Created", null);
        reworkProject.Start();
        Track(reworkProject, "Started", nameof(ProjectStatus.Planned));
        reworkProject.AdvanceStage(ProjectStage.Validation);
        Track(reworkProject, "StageChanged", nameof(ProjectStage.Planning));
        reworkProject.UpdateProgress(80);
        Track(reworkProject, "ProgressUpdated", "0");
        reworkProject.Complete();
        Track(reworkProject, "Completed", nameof(ProjectStatus.InProgress));
        timelines.Add(new ProjectTimeline(reworkProject.Id,
            CreatedAt: now.AddDays(-140), StartedAt: now.AddDays(-131),
            EndedAt: now.AddDays(-12), BlockedSince: null));

        var reworkResult = Result.Create(reworkProject.Id, actor.Id);
        reworkResult.SetEstimated(0m, 210_000m, 140_000m);
        reworkResult.SetActual(0m, 196_500m, 151_200m);
        reworkResult.SetImpactMetrics(11.5m, 640m, 18.5m);
        reworkResult.SetNotes(10, "Retrabalho da linha 3 caiu 27% no trimestre. Custo real acima do estimado por causa da calibração adicional.");
        results.Add(reworkResult);

        // 3. Running, healthy.
        var routingProject = Project.Create(
            "Roteirização dinâmica — região metropolitana",
            "Motor de roteirização diária considerando janela de entrega, ocupação e trânsito, aplicado primeiro à frota urbana.",
            people.Manager.Id, people.Manager.Name, ProjectPriority.High,
            sourceIdeaId: ideas.Routing.Id,
            linkedGuidelineId: guidelines.Logistics.Id,
            estimatedCost: 96_000m,
            deadline: now.AddDays(45));
        Track(routingProject, "Created", null);
        routingProject.Start();
        Track(routingProject, "Started", nameof(ProjectStatus.Planned));
        routingProject.AdvanceStage(ProjectStage.Execution);
        Track(routingProject, "StageChanged", nameof(ProjectStage.Planning));
        routingProject.UpdateProgress(55);
        Track(routingProject, "ProgressUpdated", "0");
        timelines.Add(new ProjectTimeline(routingProject.Id,
            CreatedAt: now.AddDays(-95), StartedAt: now.AddDays(-88),
            EndedAt: null, BlockedSince: null));

        var routingResult = Result.Create(routingProject.Id, actor.Id);
        routingResult.SetEstimated(0m, 180_000m, 96_000m);
        routingResult.SetNotes(9, "Estimativa baseada em 15% de redução no custo por entrega da frota urbana.");
        results.Add(routingResult);

        // 4. Blocked with a deadline in sight — this is what the bottleneck view is for.
        var inspectionProject = Project.Create(
            "Laudo digital de inspeção",
            "Aplicativo de inspeção em tablet com captura de foto obrigatória, assinatura do inspetor e evidência anexada ao laudo.",
            people.Manager.Id, people.Manager.Name, ProjectPriority.Medium,
            sourceIdeaId: ideas.Tablet.Id,
            linkedGuidelineId: guidelines.Inspection.Id,
            estimatedCost: 62_000m,
            deadline: now.AddDays(9));
        Track(inspectionProject, "Created", null);
        inspectionProject.Start();
        Track(inspectionProject, "Started", nameof(ProjectStatus.Planned));
        inspectionProject.UpdateProgress(35);
        Track(inspectionProject, "ProgressUpdated", "0");
        inspectionProject.Block("Fornecedor atrasou a entrega dos 12 tablets homologados; sem equipamento não há como validar em campo.");
        Track(inspectionProject, "Blocked", nameof(ProjectStatus.InProgress),
            "Fornecedor atrasou a entrega dos 12 tablets homologados; sem equipamento não há como validar em campo.");
        timelines.Add(new ProjectTimeline(inspectionProject.Id,
            CreatedAt: now.AddDays(-70), StartedAt: now.AddDays(-64),
            EndedAt: null, BlockedSince: now.AddDays(-23)));

        notifications.Add(Notification.Create(
            people.Leader.Id, NotificationType.ProjectBlocked,
            "Projeto bloqueado",
            $"\"{inspectionProject.Title}\" está bloqueado e com prazo em 9 dias.",
            $"flow://projects/{inspectionProject.Id}"));

        // 5. Planned, not started — shows the top of the funnel.
        var qualityProject = Project.Create(
            "Padronização do checklist de troca de turno",
            "Checklist digital de passagem de turno com pendências abertas e responsável designado.",
            people.Manager.Id, people.Manager.Name, ProjectPriority.Medium,
            sourceIdeaId: null,
            linkedGuidelineId: guidelines.Rework.Id,
            estimatedCost: 24_000m,
            deadline: now.AddDays(75));
        Track(qualityProject, "Created", null);
        timelines.Add(new ProjectTimeline(qualityProject.Id,
            CreatedAt: now.AddDays(-15), StartedAt: null,
            EndedAt: null, BlockedSince: null));

        // 6. Cancelled, with the reason preserved.
        var kioskProject = Project.Create(
            "Totem de autoatendimento na portaria",
            "Totem para registro de visitantes sem fila na portaria principal.",
            people.Manager.Id, people.Manager.Name, ProjectPriority.Low,
            sourceIdeaId: null,
            linkedGuidelineId: guidelines.Inspection.Id,
            estimatedCost: 38_000m,
            deadline: now.AddDays(-40));
        Track(kioskProject, "Created", null);
        kioskProject.Start();
        Track(kioskProject, "Started", nameof(ProjectStatus.Planned));
        kioskProject.UpdateProgress(20);
        Track(kioskProject, "ProgressUpdated", "0");
        kioskProject.Cancel("A portaria será reformada no próximo ciclo e o totem teria que ser reinstalado. Escopo devolvido ao backlog.");
        Track(kioskProject, "Cancelled", nameof(ProjectStatus.InProgress),
            "A portaria será reformada no próximo ciclo e o totem teria que ser reinstalado. Escopo devolvido ao backlog.");
        timelines.Add(new ProjectTimeline(kioskProject.Id,
            CreatedAt: now.AddDays(-110), StartedAt: now.AddDays(-104),
            EndedAt: now.AddDays(-62), BlockedSince: null));

        await _context.Projects.InsertManyAsync(projects, cancellationToken: ct);
        await _context.ProjectSnapshots.InsertManyAsync(snapshots, cancellationToken: ct);
        await _context.AuditLogs.InsertManyAsync(audits, cancellationToken: ct);
        await _context.Results.InsertManyAsync(results, cancellationToken: ct);
        await _context.Notifications.InsertManyAsync(notifications, cancellationToken: ct);

        foreach (var timeline in timelines)
            await ApplyTimelineAsync(timeline, ct);

        void Track(Project project, string action, string? previous, string? reason = null)
        {
            if (!projects.Contains(project)) projects.Add(project);

            snapshots.Add(ProjectSnapshot.Create(project, action, actor.Id));
            audits.Add(AuditLog.Create(
                nameof(Project), project.Id, action, actor.Id, actor.Name,
                oldValue: previous, newValue: project.Status.ToString(), reason: reason));
        }
    }

    // ---------------------------------------------------------------------------
    // Seed-only helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Rewrites createdAt so the demo has history instead of everything appearing in the
    /// same minute, which is what makes the trend chart meaningful.
    ///
    /// This writes the field directly rather than going through the domain, and it is the
    /// one place in the codebase allowed to: back-dating is not a business operation and
    /// must never be reachable from the API.
    /// </summary>
    private static Task BackdateAsync<T>(
        IMongoCollection<T> collection, Guid id, DateTimeOffset createdAt, CancellationToken ct) =>
        collection.UpdateOneAsync(
            new BsonDocument("_id", id.ToString()),
            new BsonDocument("$set", new BsonDocument("createdAt", createdAt.UtcDateTime)),
            cancellationToken: ct);

    /// <summary>
    /// Rewrites a project's lifecycle dates and spreads its audit entries and snapshots
    /// across that period.
    ///
    /// Without this the KPIs that matter most — average completion time and average days
    /// blocked — would all read zero, because every seeded transition happened in the same
    /// instant. Seed-only, like <see cref="BackdateAsync"/>.
    /// </summary>
    private async Task ApplyTimelineAsync(ProjectTimeline timeline, CancellationToken ct)
    {
        var set = new BsonDocument
        {
            ["createdAt"] = timeline.CreatedAt.UtcDateTime,
            ["updatedAt"] = (timeline.EndedAt ?? timeline.BlockedSince ?? DateTimeOffset.UtcNow).UtcDateTime
        };

        if (timeline.StartedAt is { } startedAt) set["startDate"] = startedAt.UtcDateTime;
        if (timeline.BlockedSince is { } blockedSince) set["blockedSince"] = blockedSince.UtcDateTime;

        await _context.Projects.UpdateOneAsync(
            new BsonDocument("_id", timeline.Id.ToString()),
            new BsonDocument("$set", set),
            cancellationToken: ct);

        // completedAt only exists on projects that actually completed.
        if (timeline.EndedAt is { } endedAt)
        {
            await _context.Projects.UpdateOneAsync(
                new BsonDocument
                {
                    ["_id"] = timeline.Id.ToString(),
                    ["completedAt"] = new BsonDocument("$ne", BsonNull.Value)
                },
                new BsonDocument("$set", new BsonDocument("completedAt", endedAt.UtcDateTime)),
                cancellationToken: ct);
        }

        await SpreadTimestampsAsync(timeline, ct);
    }

    private async Task SpreadTimestampsAsync(ProjectTimeline timeline, CancellationToken ct)
    {
        var last = timeline.EndedAt ?? timeline.BlockedSince ?? DateTimeOffset.UtcNow;
        var idFilter = new BsonDocument("projectId", timeline.Id.ToString());

        var snapshotIds = await _context.ProjectSnapshots
            .Find(idFilter).Project(x => x.Id).ToListAsync(ct);

        await SpreadAsync(_context.ProjectSnapshots, snapshotIds, "takenAt", timeline.CreatedAt, last, ct);

        var auditIds = await _context.AuditLogs
            .Find(new BsonDocument { ["entityType"] = "Project", ["entityId"] = timeline.Id.ToString() })
            .Project(x => x.Id).ToListAsync(ct);

        await SpreadAsync(_context.AuditLogs, auditIds, "timestamp", timeline.CreatedAt, last, ct);
    }

    private static async Task SpreadAsync<T>(
        IMongoCollection<T> collection,
        IReadOnlyList<Guid> ids,
        string field,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        if (ids.Count == 0) return;

        var span = to - from;
        for (var i = 0; i < ids.Count; i++)
        {
            var at = ids.Count == 1 ? from : from + span * ((double)i / (ids.Count - 1));

            await collection.UpdateOneAsync(
                new BsonDocument("_id", ids[i].ToString()),
                new BsonDocument("$set", new BsonDocument(field, at.UtcDateTime)),
                cancellationToken: ct);
        }
    }

    private sealed record ProjectTimeline(
        Guid Id,
        DateTimeOffset CreatedAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? EndedAt,
        DateTimeOffset? BlockedSince);

    private Task IncrementPointsAsync(Guid userId, int points, CancellationToken ct) =>
        _context.Users.UpdateOneAsync(
            Builders<User>.Filter.Eq(u => u.Id, userId),
            Builders<User>.Update.Inc(u => u.Points, points),
            cancellationToken: ct);

    private static class Narrative
    {
        public const string MarkerGuidelineTitle = "Reduzir o retrabalho na linha de montagem";
        public const string OperationalCampaign = "Onda Operacional 2026";
        public const string DigitalCampaign = "Jornada Digital 2026";
    }

    private sealed record DemoPeople(User Operator, User SecondOperator, User Manager, User Leader);

    private sealed record DemoGuidelines(
        StrategicGuideline Rework,
        StrategicGuideline Logistics,
        StrategicGuideline Inspection,
        StrategicGuideline Safety,
        StrategicGuideline Energy);

    private sealed record DemoIdeas(Idea Sensor, Idea Routing, Idea Tablet, Idea Barrier);
}
