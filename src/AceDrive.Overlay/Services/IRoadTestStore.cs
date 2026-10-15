// Road-test persistence (PORTPLAN item 49): the pick is chosen once and must
// stay the same car across overlay sessions until its road test is done, so
// the ledger's state goes to disk when a session opens and on every
// completion.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

public interface IRoadTestStore
{
    RoadTestSnapshot? Load();
    void Save(RoadTestSnapshot snapshot);
}
