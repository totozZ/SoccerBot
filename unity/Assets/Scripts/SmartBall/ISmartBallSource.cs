namespace SoccerBot
{
    public interface ISmartBallSource
    {
        string SourceName { get; }
        bool IsConnected { get; }
        SmartBallData Data { get; }
        void UpdateData();
        void ResetOrientation();
    }
}
