namespace BiampMatrixController.Models;

public enum ForcedState
{
    Default,
    ForceOn,
    ForceOff
}

public class ForcedCrosspoint
{
    public int Input { get; set; }

    public int Output { get; set; }

    public ForcedState State { get; set; }
}


public record ForceCrosspointRequest(
    int Input,
    int Output,
    ForcedState State);

public record ClearCrosspointRequest(
    int Input,
    int Output);