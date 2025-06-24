public class CircularBuffer
{
    private float[] buffer = new float[10];
    private int index = 0;
    private int count = 0;

    public void AddValue(float value)
    {
        buffer[index] = value;
        index = (index + 1) % buffer.Length;
        if (count < buffer.Length) count++;
    }

    public float GetAverage()
    {
        float sum = 0f;
        for(int i = 0; i < count; i++)
        {
            sum += buffer[i];
        }
        return count > 0 ? sum / count : 0f;
    }
}
