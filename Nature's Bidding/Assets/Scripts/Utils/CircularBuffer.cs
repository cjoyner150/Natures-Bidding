public class CircularBuffer<T>
{
    private T[] buffer;
    private readonly int bufferSize;
    
    public CircularBuffer(int size)
    {
        bufferSize = size;
        buffer = new T[bufferSize];
    }

    public void Add(T item, int index) => buffer[index % bufferSize] = item;
    public T Get(int index) => buffer[index % bufferSize];
    public void Remove(T item, int index) => buffer[index % bufferSize] = default(T);
    public void Clear() =>  buffer = new T[bufferSize];
    

}