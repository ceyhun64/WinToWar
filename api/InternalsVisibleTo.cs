using System.Runtime.CompilerServices;

// EconomyTickService.Tick gibi internal metotların api.Tests içinden reflection'a
// gerek kalmadan doğrudan çağrılabilmesi için (bkz. Bölüm 6.1 unit test gereksinimi).
[assembly: InternalsVisibleTo("api.Tests")]
