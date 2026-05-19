namespace MAPS.Shared.Enums;

public enum UserRole
{
    Admin = 1,
    Doctor = 2,
    Patient = 3
}

public enum UrgencyTier
{
    Emergency = 1,   // Risk score > 80  → Same-day alert
    Urgent    = 2,   // Risk score 60-80 → Within 24 hours
    Normal    = 3,   // Risk score 30-60 → Within 3 days
    Followup  = 4    // Risk score < 30  → Within 1 week
}

public enum InputModality
{
    Text     = 1,
    Image    = 2,
    Voice    = 3,
    Document = 4
}

public enum DiseaseType
{
    Diabetes      = 1,
    HeartDisease  = 2,
    Pneumonia     = 3,
    BrainTumour   = 4,
    SkinCancer    = 5
}

public enum PredictionStatus
{
    Pending  = 1,
    Complete = 2,
    Failed   = 3
}

public enum AppointmentStatus
{
    Booked     = 1,
    Confirmed  = 2,
    Cancelled  = 3,
    Completed  = 4,
    NoShow     = 5
}

public enum InteractionSeverity
{
    Contraindicated = 1,   // Red  — blocked until override
    Major           = 2,   // Orange — must acknowledge
    Minor           = 3    // Yellow — informational only
}

public enum TrendDirection
{
    Improving  = 1,
    Stable     = 2,
    Worsening  = 3
}

public enum ChatMessageType
{
    Text       = 1,
    Image      = 2,
    File       = 3,
    System     = 4
}

public enum RecordType
{
    Consultation  = 1,
    LabResult     = 2,
    Imaging       = 3,
    Prescription  = 4,
    Note          = 5
}
