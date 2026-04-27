using System.Collections.Generic;

namespace TTT;

public static class ListExtensions
{
	public static void Shuffle<T>( this IList<T> list )
	{
		for ( var i = list.Count - 1; i > 0; i-- )
		{
			var j = Random.Shared.Int( 0, i );
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
