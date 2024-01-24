public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100; // Adjust as needed
}

public void Main(string argument)
{
    Vector3D position = Me.GetPosition(); // Assuming this script runs on a block in the garage
    string message = $"GaragePosition|{position.X}|{position.Y}|{position.Z}";
    IGC.SendBroadcastMessage("GaragePosition", message, TransmissionDistance.TransmissionDistanceMax);
    Echo("Broadcasting garage position.");
}
