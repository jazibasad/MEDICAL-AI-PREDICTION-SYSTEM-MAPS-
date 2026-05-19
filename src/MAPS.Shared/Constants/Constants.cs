namespace MAPS.Shared.Constants;

public static class ApiRoutes
{
    public const string Base         = "api";

    public static class Auth
    {
        public const string Login    = $"{Base}/auth/login";
        public const string Register = $"{Base}/auth/register";
        public const string Refresh  = $"{Base}/auth/refresh";
        public const string Revoke   = $"{Base}/auth/revoke";
    }

    public static class Users
    {
        public const string Base     = $"{ApiRoutes.Base}/users";
        public const string ById     = $"{Base}/{{id}}";
        public const string Pending  = $"{Base}/pending";
        public const string Approve  = $"{Base}/{{id}}/approve";
    }

    public static class Assignments
    {
        public const string Base     = $"{ApiRoutes.Base}/assignments";
        public const string ById     = $"{Base}/{{id}}";
        public const string Transfer = $"{Base}/{{id}}/transfer";
    }

    public static class Patients
    {
        public const string Base     = $"{ApiRoutes.Base}/patients";
        public const string ById     = $"{Base}/{{id}}";
        public const string Timeline = $"{Base}/{{id}}/timeline";
    }

    public static class Predictions
    {
        public const string Base         = $"{ApiRoutes.Base}/predictions";
        public const string ById         = $"{Base}/{{id}}";
        public const string Differential = $"{Base}/differential";
    }

    public static class Images
    {
        public const string Analyze  = $"{ApiRoutes.Base}/images/analyze";
        public const string ById     = $"{ApiRoutes.Base}/images/{{id}}";
    }

    public static class Risks
    {
        public const string ByPatient = $"{ApiRoutes.Base}/risks/patient/{{id}}";
        public const string Alerts    = $"{ApiRoutes.Base}/risks/alerts";
    }

    public static class Notes
    {
        public const string Base     = $"{ApiRoutes.Base}/notes";
        public const string Entities = $"{ApiRoutes.Base}/notes/{{id}}/entities";
    }

    public static class Voice
    {
        public const string Transcribe = $"{ApiRoutes.Base}/voice/transcribe";
    }

    public static class Chatbot
    {
        public const string Query   = $"{ApiRoutes.Base}/chatbot/query";
        public const string Session = $"{ApiRoutes.Base}/chatbot/session";
        public const string History = $"{ApiRoutes.Base}/chatbot/history";
    }

    public static class Literature
    {
        public const string Search  = $"{ApiRoutes.Base}/literature/search";
    }

    public static class Drugs
    {
        public const string Check   = $"{ApiRoutes.Base}/drugs/check-interactions";
    }

    public static class Appointments
    {
        public const string Base    = $"{ApiRoutes.Base}/appointments";
        public const string ById    = $"{Base}/{{id}}";
    }

    public static class Prescriptions
    {
        public const string Base    = $"{ApiRoutes.Base}/prescriptions";
        public const string ById    = $"{Base}/{{id}}";
    }

    public static class Feedback
    {
        public const string Base    = $"{ApiRoutes.Base}/feedback";
    }

    public static class Analytics
    {
        public const string Dashboard   = $"{ApiRoutes.Base}/analytics/dashboard";
        public const string Predictions = $"{ApiRoutes.Base}/analytics/predictions";
        public const string Users       = $"{ApiRoutes.Base}/analytics/users";
    }

    public static class System
    {
        public const string Containers  = $"{ApiRoutes.Base}/system/containers";
        public const string Health      = $"{ApiRoutes.Base}/system/health";
        public const string Pods        = $"{ApiRoutes.Base}/system/pods";
        public const string Deployments = $"{ApiRoutes.Base}/system/deployments";
        public const string Hpa         = $"{ApiRoutes.Base}/system/hpa";
    }
}

public static class PolicyNames
{
    public const string AdminOnly        = "AdminOnly";
    public const string DoctorOnly       = "DoctorOnly";
    public const string PatientOnly      = "PatientOnly";
    public const string DoctorOrAdmin    = "DoctorOrAdmin";
    public const string AnyAuthenticatedUser = "AnyAuthenticatedUser";
}

public static class ClaimTypeNames
{
    public const string UserId     = "uid";
    public const string Role       = "role";
    public const string Email      = "email";
    public const string FullName   = "fullname";
    public const string IsApproved = "approved";
}

public static class CacheKeys
{
    public const string UserById        = "user:{0}";
    public const string AssignmentsByDoc = "assignments:doctor:{0}";
    public const string RiskByPatient   = "risk:patient:{0}";
    public const string DashboardStats  = "dashboard:stats";
}

public static class HangfireQueues
{
    public const string Default         = "default";
    public const string RiskCalc        = "risk-calculation";
    public const string ModelRetrain    = "model-retrain";
    public const string Notifications   = "notifications";
}
