namespace MontBlanc.device;

internal interface IMcu
{
	bool findDevice();

	bool eraseFlash(uint start, uint end, IProgress c);

	bool eraseExtFlash(uint start, uint end, uint blockSize, IProgress p);

	bool programFlash(uint start, uint end, byte[] flashData, IProgress p);

	bool programExtFlash(uint start, uint end, byte[] flashData);

	bool programExtFlash(uint start, uint end, uint blockSize, byte[] flashData, IProgress p);

	bool readFlash(uint start, uint end, byte[] flashData);

	byte[] readExtFlash(uint start, uint end, uint blockSize, IProgress p);

	bool rebootDevice(int mode);
}
