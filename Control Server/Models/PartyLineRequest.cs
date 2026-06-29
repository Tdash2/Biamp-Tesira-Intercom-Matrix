namespace BiampMatrixController.Models;

public class PartyLineRequest
{
    public int PlId { get; set; }
    public int Input { get; set; }
    public int Output { get; set; }

}

public record RenameRequest(int PlId, string Name);

public record DeleteRequest(int PlId);