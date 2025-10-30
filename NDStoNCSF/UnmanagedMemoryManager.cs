using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NDStoNCSF;

// The first class, UnmanagedMemoryManager, is derived from https://github.com/mgravell/Pipelines.Sockets.Unofficial but mostly just
// copied here instead of using the package to clean it up a bit, plus the package is a tad old now (its highest .NET was .NET 5).
// The second set of classes, ultimately ending with UnmanagedReadOnlyMemoryManager, is partially based on .NET's source code and
// partially just an extension of the first class. It uses reflection to create a ReadOnlyMemory<T>, though, so it is going to be far
// more likely to fail in the future if the internals of ReadOnlyMemory<T> ever change. So far, they have stayed the same since at least
// .NET 5.

public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
{
	readonly void* _pointer;
	readonly int _length;

	public UnmanagedMemoryManager(Span<T> span)
	{
		this._pointer = Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
		this._length = span.Length;
	}

	public UnmanagedMemoryManager(T* pointer, int length) : this((void*)pointer, length)
	{
	}

	public UnmanagedMemoryManager(nint pointer, int length) : this((void*)pointer, length)
	{
	}

	UnmanagedMemoryManager(void* pointer, int length)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(length, 0);
		this._pointer = pointer;
		this._length = length;
	}

	public override Span<T> GetSpan() => new(this._pointer, this._length);

	public override MemoryHandle Pin(int elementIndex = 0)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(elementIndex, 0);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elementIndex, this._length);
		return new(Unsafe.Add<T>(this._pointer, elementIndex));
	}

	public override void Unpin()
	{
	}

	protected override void Dispose(bool disposing)
	{
	}
}

/// <summary>
/// Owner of <see cref="ReadOnlyMemory{T}" /> that is responsible for disposing the underlying memory appropriately.
/// </summary>
public interface IReadOnlyMemoryOwner<T> : IDisposable
{
	/// <summary>
	/// Returns a <see cref="ReadOnlyMemory{T}" />.
	/// </summary>
	ReadOnlyMemory<T> Memory { get; }
}
/// <summary>
/// Manager of <see cref="ReadOnlyMemory{T}" /> that provides the implementation.
/// </summary>
public abstract class ReadOnlyMemoryManager<T> : IReadOnlyMemoryOwner<T>, IPinnable
{
	// This most likely will only work as long as ReadOnlyMemory<T>'s internals stay the same, so really no guarantee at all.
	// It is at least valid with .NET 9 but would've been valid all the way back to at least .NET 5.
	static readonly Lazy<Func<ReadOnlyMemoryManager<T>, int, ReadOnlyMemory<T>>> createMemory = new(static () =>
	{
		var romType = typeof(ReadOnlyMemory<T>);
		var memoryManagerVariableExpression = Expression.Variable(typeof(ReadOnlyMemoryManager<T>), "uromm");
		var lengthVariableExpression = Expression.Variable(typeof(int), "length");
		return Expression.Lambda<Func<ReadOnlyMemoryManager<T>, int, ReadOnlyMemory<T>>>(
			Expression.MemberInit(
				Expression.New(romType),
				Expression.Bind(romType.GetField("_object", BindingFlags.NonPublic | BindingFlags.Instance)!, memoryManagerVariableExpression),
				Expression.Bind(romType.GetField("_length", BindingFlags.NonPublic | BindingFlags.Instance)!, lengthVariableExpression)
			),
			memoryManagerVariableExpression,
			lengthVariableExpression
		).Compile();
	});

	/// <summary>
	/// Returns a <see cref="ReadOnlyMemory{T}" />.
	/// </summary>
	public virtual ReadOnlyMemory<T> Memory => ReadOnlyMemoryManager<T>.createMemory.Value(this, this.GetSpan().Length);

	/// <summary>
	/// Returns a span wrapping the underlying memory.
	/// </summary>
	public abstract ReadOnlySpan<T> GetSpan();

	/// <summary>
	/// Returns a handle to the memory that has been pinned and hence its address can be taken.
	/// </summary>
	/// <param name="elementIndex">
	/// The offset to the element within the memory at which the returned <see cref="MemoryHandle" /> points to. (default = 0)
	/// </param>
	public abstract MemoryHandle Pin(int elementIndex = 0);

	/// <summary>
	/// Lets the garbage collector know that the object is free to be moved now.
	/// </summary>
	public abstract void Unpin();

	/// <summary>
	/// Returns an array segment.
	/// <remarks>Returns the default array segment if not overridden.</remarks>
	/// </summary>
	protected internal virtual bool TryGetArray(out ArraySegment<T> segment)
	{
		segment = default;
		return false;
	}

	/// <summary>
	/// Implements IDisposable.
	/// </summary>
	void IDisposable.Dispose()
	{
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Clean up of any leftover managed and unmanaged resources.
	/// </summary>
	protected abstract void Dispose(bool disposing);
}
public sealed unsafe class UnmanagedReadOnlyMemoryManager<T> : ReadOnlyMemoryManager<T> where T : unmanaged
{
	readonly void* _pointer;
	readonly int _length;

	public UnmanagedReadOnlyMemoryManager(ReadOnlySpan<T> span)
	{
		this._pointer = Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
		this._length = span.Length;
	}

	public UnmanagedReadOnlyMemoryManager(T* pointer, int length) : this((void*)pointer, length)
	{
	}

	public UnmanagedReadOnlyMemoryManager(nint pointer, int length) : this((void*)pointer, length)
	{
	}

	UnmanagedReadOnlyMemoryManager(void* pointer, int length)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(length, 0);
		this._pointer = pointer;
		this._length = length;
	}

	public override ReadOnlySpan<T> GetSpan() => new(this._pointer, this._length);

	public override MemoryHandle Pin(int elementIndex = 0)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(elementIndex, 0);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elementIndex, this._length);
		return new(Unsafe.Add<T>(this._pointer, elementIndex));
	}

	public override void Unpin()
	{
	}

	protected override void Dispose(bool disposing)
	{
	}
}
