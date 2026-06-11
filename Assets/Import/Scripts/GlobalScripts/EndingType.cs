public enum EndingType
{
    None        = 0,
    TrueEnding  = 1,  // 6 записок + все пациенты спасены
    FalseEnding = 2,  // <6 записок + 3+ пациентов спасено
    BadEnding   = 3,  // 6 записок + не все пациенты спасены
    Failure     = 4,  // <6 записок + ≤2 пациентов спасено
}
