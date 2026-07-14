namespace SingBoxServer.Services.Generators.SingBox.Patchers;

internal interface IConfigPatcher
{
    bool CanPatch(string? device);
    SingBoxTemplate ApplyPatch(SingBoxTemplate config);
}
