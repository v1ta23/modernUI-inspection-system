using App.Core.Interfaces;
using App.Core.Services;
using App.Infrastructure.Config;
using App.Infrastructure.Repositories;
using App.WinForms.Controllers;
using App.WinForms.Exports;
using App.WinForms.Views;

namespace App.WinForms;

internal sealed class AppCompositionRoot
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IDashboardService _dashboardService;
    private readonly IInspectionRecordService _inspectionRecordService;

    public AppCompositionRoot()
    {
        var sqlOptions = new SqlServerOptions
        {
            ConnectionString = "Server=localhost;Database=TestDB;Trusted_Connection=True;TrustServerCertificate=True;"
        };

        var rememberMePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remember.txt");
        var userRepository = new SqlUserRepository(sqlOptions);
        var rememberMeRepository = new FileRememberMeRepository(rememberMePath);
        var dashboardRepository = new DemoDashboardRepository();
        var inspectionRecordPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "inspection-records.json");
        var inspectionTemplatePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "inspection-templates.json");
        var inspectionRecordRepository = new JsonInspectionRecordRepository(inspectionRecordPath);
        var inspectionTemplateRepository = new JsonInspectionTemplateRepository(inspectionTemplatePath);

        _authenticationService = new AuthenticationService(userRepository, rememberMeRepository);
        _dashboardService = new DashboardService(dashboardRepository);
        _inspectionRecordService = new InspectionRecordService(
            inspectionRecordRepository,
            inspectionTemplateRepository);
    }

    public LoginForm CreateLoginForm()
    {
        return new LoginForm(new LoginController(_authenticationService), this);
    }

    public RegisterForm CreateRegisterForm()
    {
        return new RegisterForm(new RegisterController(_authenticationService));
    }

    public MainForm CreateDashboardForm(string account)
    {
        var dashboardController = new DashboardController(_dashboardService);
        var inspectionController = new InspectionController(
            _inspectionRecordService,
            new InspectionExcelExporter());
        return new MainForm(
            dashboardController.Load(account),
            inspectionController,
            account);
    }
}
