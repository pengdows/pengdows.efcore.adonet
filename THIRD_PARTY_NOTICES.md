# Third-party notices

Production source contains clean-room reimplementations informed by the following compatible sources. The specification-test
project also retains a small number of explicitly attributed MIT/PostgreSQL test-fixture adaptations; those fixtures are not
shipped in provider packages and remain tracked here until their equivalent shared fixtures are independently rewritten.

- Entity Framework Core, MIT License, .NET Foundation: <https://github.com/dotnet/efcore/tree/v10.0.11>
- Pomelo.EntityFrameworkCore.MySql, MIT License, Pomelo Foundation: <https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/tree/14c2cd558e37c71e7a2fca75e339ef902effdf60>
- Npgsql.EntityFrameworkCore.PostgreSQL, PostgreSQL License: <https://github.com/npgsql/efcore.pg/tree/v10.0.3>

Files adapted from a source retain the source copyright and license attribution in their headers. Production comments marked
“Behaviour reference (not copied)” describe independent implementations and are not copied source code. Oracle's GPL
implementation is never copied; it is used only as a behavior reference where noted in tests.
