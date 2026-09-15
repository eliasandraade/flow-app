using MediatR;

namespace Flow.Application.Dashboard.Queries.GetDashboardSummary;

public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;
