namespace MontBlanc;

internal interface IProgress
{
	bool updateProgress(int percent, int stage);

	bool setLable(string info);
}
