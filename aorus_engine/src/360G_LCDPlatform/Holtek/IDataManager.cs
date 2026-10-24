using System.IO;
using SevenZip;

namespace Holtek;

internal interface IDataManager
{
	Stream compress(Stream input, ICodeProgress icp);

	Stream decompress(Stream input);

	Stream crypto(Stream input, byte[] key);

	Stream decrypt(Stream input, byte[] key);
}
