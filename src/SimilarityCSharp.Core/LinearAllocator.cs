using System.Buffers;
using System.Runtime.CompilerServices;

namespace SimilarityCSharp;

ref struct LinearAllocator<T>(int sizeHint = 32)
{
    T[]? buffer = ArrayPool<T>.Shared.Rent(sizeHint);
    int tail;

    public bool IsDisposed => tail == -1;
    public int Count => tail;
    
    public Span<T> Allocate(int toAlloc)
    {
        ThrowIfDisposed();
        var newSize =  tail + toAlloc;
        if (buffer == null || buffer.Length < newSize)
        {
            var newArray = ArrayPool<T>.Shared.Rent(newSize);
            if (buffer != null)
            {
                buffer.AsSpan().CopyTo(newArray);
                ArrayPool<T>.Shared.Return(buffer);
            }
            buffer = newArray;
        }
        var span = new Span<T>(buffer, tail, toAlloc);
        tail += toAlloc;
        return span;
    }
    
    public void Deallocate(int count)
    {
        ThrowIfDisposed();
        if (count < 0 || count > tail) throw new ArgumentOutOfRangeException(nameof(count), "Cannot deallocate more than allocated or negative count.");
        tail -= count;
    }
    
    public void Clear()
    {
        ThrowIfDisposed();

        if (buffer != null)
        {
            new Span<T>(buffer, 0, tail).Clear();
        }

        tail = 0;
    }

    public void Dispose()
    {
        ThrowIfDisposed();

        if (buffer != null)
        {
            ArrayPool<T>.Shared.Return(buffer);
            buffer = null;
        }

        tail = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsSpan()
    {
        return new ReadOnlySpan<T>(buffer, 0, tail);
    }

    void ThrowIfDisposed()
    {
        if (tail == -1) ThrowDisposedException();
    }
    
    void ThrowDisposedException()
    {
        throw new ObjectDisposedException(nameof(LinearAllocator<T>));
    }
    
}