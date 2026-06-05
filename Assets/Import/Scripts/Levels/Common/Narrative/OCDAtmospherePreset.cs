/// <summary>
/// Пресеты атмосферы для цепочки <see cref="OCDMomentTrigger"/> (комната → улица → вечер дома).
/// </summary>
public enum OCDAtmospherePreset
{
  None = 0,
  /// <summary>Утро/день в квартире (старт сцены).</summary>
  HomeMorning = 1,
  /// <summary>Днём на улице (пока герой «ушёл» из дома).</summary>
  OutdoorDay = 2,
  /// <summary>Вечер, возвращение домой.</summary>
  HomeEvening = 3,
  /// <summary>Ночь на улице (долго сидел за работой, снаружи уже темно).</summary>
  OutdoorNight = 4
}
