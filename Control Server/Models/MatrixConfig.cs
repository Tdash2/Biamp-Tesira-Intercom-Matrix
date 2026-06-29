using BiampMatrixController.Models;

public class MatrixConfig
{
    public List<PartyLine> PartyLines { get; set; } = new();
    public List<ForcedCrosspoint> ForcedCrosspoints { get; set; } = new();
}