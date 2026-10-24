using System;
using System.IO;
using SevenZip.Compression.LZ;
using SevenZip.Compression.RangeCoder;

namespace SevenZip.Compression.LZMA;

public class Encoder : ICoder, ISetCoderProperties, IWriteCoderProperties
{
	private enum EMatchFinderType
	{
		BT2,
		BT4
	}

	private class LiteralEncoder
	{
		public struct Encoder2
		{
			private BitEncoder[] m_Encoders;

			public void Create()
			{
				m_Encoders = new BitEncoder[768];
			}

			public void Init()
			{
				for (int i = 0; i < 768; i++)
				{
					m_Encoders[i].Init();
				}
			}

			public void Encode(SevenZip.Compression.RangeCoder.Encoder rangeEncoder, byte symbol)
			{
				uint num = 1u;
				for (int num2 = 7; num2 >= 0; num2--)
				{
					uint num3 = (uint)((symbol >> num2) & 1);
					m_Encoders[num].Encode(rangeEncoder, num3);
					num = (num << 1) | num3;
				}
			}

			public void EncodeMatched(SevenZip.Compression.RangeCoder.Encoder rangeEncoder, byte matchByte, byte symbol)
			{
				uint num = 1u;
				bool flag = true;
				for (int num2 = 7; num2 >= 0; num2--)
				{
					uint num3 = (uint)((symbol >> num2) & 1);
					uint num4 = num;
					if (flag)
					{
						uint num5 = (uint)((matchByte >> num2) & 1);
						num4 += 1 + num5 << 8;
						flag = num5 == num3;
					}
					m_Encoders[num4].Encode(rangeEncoder, num3);
					num = (num << 1) | num3;
				}
			}

			public uint GetPrice(bool matchMode, byte matchByte, byte symbol)
			{
				uint num = 0u;
				uint num2 = 1u;
				int num3 = 7;
				if (matchMode)
				{
					while (num3 >= 0)
					{
						uint num4 = (uint)((matchByte >> num3) & 1);
						uint num5 = (uint)((symbol >> num3) & 1);
						num += m_Encoders[(1 + num4 << 8) + num2].GetPrice(num5);
						num2 = (num2 << 1) | num5;
						if (num4 != num5)
						{
							num3--;
							break;
						}
						num3--;
					}
				}
				while (num3 >= 0)
				{
					uint num5 = (uint)((symbol >> num3) & 1);
					num += m_Encoders[num2].GetPrice(num5);
					num2 = (num2 << 1) | num5;
					num3--;
				}
				return num;
			}
		}

		private Encoder2[] m_Coders;

		private int m_NumPrevBits;

		private int m_NumPosBits;

		private uint m_PosMask;

		public void Create(int numPosBits, int numPrevBits)
		{
			if (m_Coders == null || m_NumPrevBits != numPrevBits || m_NumPosBits != numPosBits)
			{
				m_NumPosBits = numPosBits;
				m_PosMask = (uint)((1 << numPosBits) - 1);
				m_NumPrevBits = numPrevBits;
				uint num = (uint)(1 << m_NumPrevBits + m_NumPosBits);
				m_Coders = new Encoder2[num];
				for (uint num2 = 0u; num2 < num; num2++)
				{
					m_Coders[num2].Create();
				}
			}
		}

		public void Init()
		{
			uint num = (uint)(1 << m_NumPrevBits + m_NumPosBits);
			for (uint num2 = 0u; num2 < num; num2++)
			{
				m_Coders[num2].Init();
			}
		}

		public Encoder2 GetSubCoder(uint pos, byte prevByte)
		{
			return m_Coders[(int)((pos & m_PosMask) << m_NumPrevBits) + (prevByte >> 8 - m_NumPrevBits)];
		}
	}

	private class LenEncoder
	{
		private BitEncoder _choice = default;

		private BitEncoder _choice2 = default;

		private BitTreeEncoder[] _lowCoder = new BitTreeEncoder[16];

		private BitTreeEncoder[] _midCoder = new BitTreeEncoder[16];

		private BitTreeEncoder _highCoder = new BitTreeEncoder(8);

		public LenEncoder()
		{
			for (uint num = 0u; num < 16; num++)
			{
				ref BitTreeEncoder reference = ref _lowCoder[num];
				reference = new BitTreeEncoder(3);
				ref BitTreeEncoder reference2 = ref _midCoder[num];
				reference2 = new BitTreeEncoder(3);
			}
		}

		public void Init(uint numPosStates)
		{
			_choice.Init();
			_choice2.Init();
			for (uint num = 0u; num < numPosStates; num++)
			{
				_lowCoder[num].Init();
				_midCoder[num].Init();
			}
			_highCoder.Init();
		}

		public void Encode(SevenZip.Compression.RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
		{
			if (symbol < 8)
			{
				_choice.Encode(rangeEncoder, 0u);
				_lowCoder[posState].Encode(rangeEncoder, symbol);
				return;
			}
			symbol -= 8;
			_choice.Encode(rangeEncoder, 1u);
			if (symbol < 8)
			{
				_choice2.Encode(rangeEncoder, 0u);
				_midCoder[posState].Encode(rangeEncoder, symbol);
			}
			else
			{
				_choice2.Encode(rangeEncoder, 1u);
				_highCoder.Encode(rangeEncoder, symbol - 8);
			}
		}

		public void SetPrices(uint posState, uint numSymbols, uint[] prices, uint st)
		{
			uint price = _choice.GetPrice0();
			uint price2 = _choice.GetPrice1();
			uint num = price2 + _choice2.GetPrice0();
			uint num2 = price2 + _choice2.GetPrice1();
			uint num3 = 0u;
			for (num3 = 0u; num3 < 8; num3++)
			{
				if (num3 >= numSymbols)
				{
					return;
				}
				prices[st + num3] = price + _lowCoder[posState].GetPrice(num3);
			}
			for (; num3 < 16; num3++)
			{
				if (num3 >= numSymbols)
				{
					return;
				}
				prices[st + num3] = num + _midCoder[posState].GetPrice(num3 - 8);
			}
			for (; num3 < numSymbols; num3++)
			{
				prices[st + num3] = num2 + _highCoder.GetPrice(num3 - 8 - 8);
			}
		}
	}

	private class LenPriceTableEncoder : LenEncoder
	{
		private uint[] _prices = new uint[4352];

		private uint _tableSize;

		private uint[] _counters = new uint[16];

		public void SetTableSize(uint tableSize)
		{
			_tableSize = tableSize;
		}

		public uint GetPrice(uint symbol, uint posState)
		{
			return _prices[posState * 272 + symbol];
		}

		private void UpdateTable(uint posState)
		{
			SetPrices(posState, _tableSize, _prices, posState * 272);
			_counters[posState] = _tableSize;
		}

		public void UpdateTables(uint numPosStates)
		{
			for (uint num = 0u; num < numPosStates; num++)
			{
				UpdateTable(num);
			}
		}

		public new void Encode(SevenZip.Compression.RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
		{
			base.Encode(rangeEncoder, symbol, posState);
			if (--_counters[posState] == 0)
			{
				UpdateTable(posState);
			}
		}
	}

	private class Optimal
	{
		public Base.State State;

		public bool Prev1IsChar;

		public bool Prev2;

		public uint PosPrev2;

		public uint BackPrev2;

		public uint Price;

		public uint PosPrev;

		public uint BackPrev;

		public uint Backs0;

		public uint Backs1;

		public uint Backs2;

		public uint Backs3;

		public void MakeAsChar()
		{
			BackPrev = uint.MaxValue;
			Prev1IsChar = false;
		}

		public void MakeAsShortRep()
		{
			BackPrev = 0u;
			Prev1IsChar = false;
		}

		public bool IsShortRep()
		{
			return BackPrev == 0;
		}
	}

	private const uint kIfinityPrice = 268435455u;

	private const int kDefaultDictionaryLogSize = 22;

	private const uint kNumFastBytesDefault = 32u;

	private const uint kNumLenSpecSymbols = 16u;

	private const uint kNumOpts = 4096u;

	private const int kPropSize = 5;

	private static byte[] g_FastPos;

	private Base.State _state = default;

	private byte _previousByte;

	private uint[] _repDistances = new uint[4];

	private Optimal[] _optimum = new Optimal[4096];

	private IMatchFinder _matchFinder = null;

	private SevenZip.Compression.RangeCoder.Encoder _rangeEncoder = new SevenZip.Compression.RangeCoder.Encoder();

	private BitEncoder[] _isMatch = new BitEncoder[192];

	private BitEncoder[] _isRep = new BitEncoder[12];

	private BitEncoder[] _isRepG0 = new BitEncoder[12];

	private BitEncoder[] _isRepG1 = new BitEncoder[12];

	private BitEncoder[] _isRepG2 = new BitEncoder[12];

	private BitEncoder[] _isRep0Long = new BitEncoder[192];

	private BitTreeEncoder[] _posSlotEncoder = new BitTreeEncoder[4];

	private BitEncoder[] _posEncoders = new BitEncoder[114];

	private BitTreeEncoder _posAlignEncoder = new BitTreeEncoder(4);

	private LenPriceTableEncoder _lenEncoder = new LenPriceTableEncoder();

	private LenPriceTableEncoder _repMatchLenEncoder = new LenPriceTableEncoder();

	private LiteralEncoder _literalEncoder = new LiteralEncoder();

	private uint[] _matchDistances = new uint[548];

	private uint _numFastBytes = 32u;

	private uint _longestMatchLength;

	private uint _numDistancePairs;

	private uint _additionalOffset;

	private uint _optimumEndIndex;

	private uint _optimumCurrentIndex;

	private bool _longestMatchWasFound;

	private uint[] _posSlotPrices = new uint[256];

	private uint[] _distancesPrices = new uint[512];

	private uint[] _alignPrices = new uint[16];

	private uint _alignPriceCount;

	private uint _distTableSize = 44u;

	private int _posStateBits = 2;

	private uint _posStateMask = 3u;

	private int _numLiteralPosStateBits = 0;

	private int _numLiteralContextBits = 3;

	private uint _dictionarySize = 4194304u;

	private uint _dictionarySizePrev = uint.MaxValue;

	private uint _numFastBytesPrev = uint.MaxValue;

	private long nowPos64;

	private bool _finished;

	private Stream _inStream;

	private EMatchFinderType _matchFinderType = EMatchFinderType.BT4;

	private bool _writeEndMark = false;

	private bool _needReleaseMFStream;

	private uint[] reps = new uint[4];

	private uint[] repLens = new uint[4];

	private byte[] properties = new byte[5];

	private uint[] tempPrices = new uint[128];

	private uint _matchPriceCount;

	private static string[] kMatchFinderIDs;

	private uint _trainSize = 0u;

	static Encoder()
	{
		g_FastPos = new byte[2048];
		kMatchFinderIDs = new string[2] { "BT2", "BT4" };
		int num = 2;
		g_FastPos[0] = 0;
		g_FastPos[1] = 1;
		for (byte b = 2; b < 22; b++)
		{
			uint num2 = (uint)(1 << (b >> 1) - 1);
			uint num3 = 0u;
			while (num3 < num2)
			{
				g_FastPos[num] = b;
				num3++;
				num++;
			}
		}
	}

	private static uint GetPosSlot(uint pos)
	{
		if (pos < 2048)
		{
			return g_FastPos[pos];
		}
		if (pos < 2097152)
		{
			return (uint)(g_FastPos[pos >> 10] + 20);
		}
		return (uint)(g_FastPos[pos >> 20] + 40);
	}

	private static uint GetPosSlot2(uint pos)
	{
		if (pos < 131072)
		{
			return (uint)(g_FastPos[pos >> 6] + 12);
		}
		if (pos < 134217728)
		{
			return (uint)(g_FastPos[pos >> 16] + 32);
		}
		return (uint)(g_FastPos[pos >> 26] + 52);
	}

	private void BaseInit()
	{
		_state.Init();
		_previousByte = 0;
		for (uint num = 0u; num < 4; num++)
		{
			_repDistances[num] = 0u;
		}
	}

	private void Create()
	{
		if (_matchFinder == null)
		{
			BinTree binTree = new BinTree();
			int type = 4;
			if (_matchFinderType == EMatchFinderType.BT2)
			{
				type = 2;
			}
			binTree.SetType(type);
			_matchFinder = binTree;
		}
		_literalEncoder.Create(_numLiteralPosStateBits, _numLiteralContextBits);
		if (_dictionarySize != _dictionarySizePrev || _numFastBytesPrev != _numFastBytes)
		{
			_matchFinder.Create(_dictionarySize, 4096u, _numFastBytes, 274u);
			_dictionarySizePrev = _dictionarySize;
			_numFastBytesPrev = _numFastBytes;
		}
	}

	public Encoder()
	{
		for (int i = 0; (long)i < 4096L; i++)
		{
			_optimum[i] = new Optimal();
		}
		for (int i = 0; (long)i < 4L; i++)
		{
			ref BitTreeEncoder reference = ref _posSlotEncoder[i];
			reference = new BitTreeEncoder(6);
		}
	}

	private void SetWriteEndMarkerMode(bool writeEndMarker)
	{
		_writeEndMark = writeEndMarker;
	}

	private void Init()
	{
		BaseInit();
		_rangeEncoder.Init();
		for (uint num = 0u; num < 12; num++)
		{
			for (uint num2 = 0u; num2 <= _posStateMask; num2++)
			{
				uint num3 = (num << 4) + num2;
				_isMatch[num3].Init();
				_isRep0Long[num3].Init();
			}
			_isRep[num].Init();
			_isRepG0[num].Init();
			_isRepG1[num].Init();
			_isRepG2[num].Init();
		}
		_literalEncoder.Init();
		for (uint num = 0u; num < 4; num++)
		{
			_posSlotEncoder[num].Init();
		}
		for (uint num = 0u; num < 114; num++)
		{
			_posEncoders[num].Init();
		}
		_lenEncoder.Init((uint)(1 << _posStateBits));
		_repMatchLenEncoder.Init((uint)(1 << _posStateBits));
		_posAlignEncoder.Init();
		_longestMatchWasFound = false;
		_optimumEndIndex = 0u;
		_optimumCurrentIndex = 0u;
		_additionalOffset = 0u;
	}

	private void ReadMatchDistances(out uint lenRes, out uint numDistancePairs)
	{
		lenRes = 0u;
		numDistancePairs = _matchFinder.GetMatches(_matchDistances);
		if (numDistancePairs != 0)
		{
			lenRes = _matchDistances[numDistancePairs - 2];
			if (lenRes == _numFastBytes)
			{
				lenRes += _matchFinder.GetMatchLen((int)(lenRes - 1), _matchDistances[numDistancePairs - 1], 273 - lenRes);
			}
		}
		_additionalOffset++;
	}

	private void MovePos(uint num)
	{
		if (num != 0)
		{
			_matchFinder.Skip(num);
			_additionalOffset += num;
		}
	}

	private uint GetRepLen1Price(Base.State state, uint posState)
	{
		return _isRepG0[state.Index].GetPrice0() + _isRep0Long[(state.Index << 4) + posState].GetPrice0();
	}

	private uint GetPureRepPrice(uint repIndex, Base.State state, uint posState)
	{
		uint price;
		if (repIndex == 0)
		{
			price = _isRepG0[state.Index].GetPrice0();
			return price + _isRep0Long[(state.Index << 4) + posState].GetPrice1();
		}
		price = _isRepG0[state.Index].GetPrice1();
		if (repIndex == 1)
		{
			return price + _isRepG1[state.Index].GetPrice0();
		}
		price += _isRepG1[state.Index].GetPrice1();
		return price + _isRepG2[state.Index].GetPrice(repIndex - 2);
	}

	private uint GetRepPrice(uint repIndex, uint len, Base.State state, uint posState)
	{
		uint price = _repMatchLenEncoder.GetPrice(len - 2, posState);
		return price + GetPureRepPrice(repIndex, state, posState);
	}

	private uint GetPosLenPrice(uint pos, uint len, uint posState)
	{
		uint lenToPosState = Base.GetLenToPosState(len);
		uint num = ((pos >= 128) ? (_posSlotPrices[(lenToPosState << 6) + GetPosSlot2(pos)] + _alignPrices[pos & 0xF]) : _distancesPrices[lenToPosState * 128 + pos]);
		return num + _lenEncoder.GetPrice(len - 2, posState);
	}

	private uint Backward(out uint backRes, uint cur)
	{
		_optimumEndIndex = cur;
		uint posPrev = _optimum[cur].PosPrev;
		uint backPrev = _optimum[cur].BackPrev;
		do
		{
			if (_optimum[cur].Prev1IsChar)
			{
				_optimum[posPrev].MakeAsChar();
				_optimum[posPrev].PosPrev = posPrev - 1;
				if (_optimum[cur].Prev2)
				{
					_optimum[posPrev - 1].Prev1IsChar = false;
					_optimum[posPrev - 1].PosPrev = _optimum[cur].PosPrev2;
					_optimum[posPrev - 1].BackPrev = _optimum[cur].BackPrev2;
				}
			}
			uint num = posPrev;
			uint backPrev2 = backPrev;
			backPrev = _optimum[num].BackPrev;
			posPrev = _optimum[num].PosPrev;
			_optimum[num].BackPrev = backPrev2;
			_optimum[num].PosPrev = cur;
			cur = num;
		}
		while (cur != 0);
		backRes = _optimum[0].BackPrev;
		_optimumCurrentIndex = _optimum[0].PosPrev;
		return _optimumCurrentIndex;
	}

	private uint GetOptimum(uint position, out uint backRes)
	{
		if (_optimumEndIndex != _optimumCurrentIndex)
		{
			uint result = _optimum[_optimumCurrentIndex].PosPrev - _optimumCurrentIndex;
			backRes = _optimum[_optimumCurrentIndex].BackPrev;
			_optimumCurrentIndex = _optimum[_optimumCurrentIndex].PosPrev;
			return result;
		}
		_optimumCurrentIndex = (_optimumEndIndex = 0u);
		uint lenRes;
		uint numDistancePairs;
		if (!_longestMatchWasFound)
		{
			ReadMatchDistances(out lenRes, out numDistancePairs);
		}
		else
		{
			lenRes = _longestMatchLength;
			numDistancePairs = _numDistancePairs;
			_longestMatchWasFound = false;
		}
		uint num = _matchFinder.GetNumAvailableBytes() + 1;
		if (num < 2)
		{
			backRes = uint.MaxValue;
			return 1u;
		}
		if (num > 273)
		{
			num = 273u;
		}
		uint num2 = 0u;
		for (uint num3 = 0u; num3 < 4; num3++)
		{
			reps[num3] = _repDistances[num3];
			repLens[num3] = _matchFinder.GetMatchLen(-1, reps[num3], 273u);
			if (repLens[num3] > repLens[num2])
			{
				num2 = num3;
			}
		}
		if (repLens[num2] >= _numFastBytes)
		{
			backRes = num2;
			uint result = repLens[num2];
			MovePos(result - 1);
			return result;
		}
		if (lenRes >= _numFastBytes)
		{
			backRes = _matchDistances[numDistancePairs - 1] + 4;
			MovePos(lenRes - 1);
			return lenRes;
		}
		byte indexByte = _matchFinder.GetIndexByte(-1);
		byte indexByte2 = _matchFinder.GetIndexByte((int)(0 - _repDistances[0] - 1 - 1));
		if (lenRes < 2 && indexByte != indexByte2 && repLens[num2] < 2)
		{
			backRes = uint.MaxValue;
			return 1u;
		}
		_optimum[0].State = _state;
		uint num4 = position & _posStateMask;
		_optimum[1].Price = _isMatch[(_state.Index << 4) + num4].GetPrice0() + _literalEncoder.GetSubCoder(position, _previousByte).GetPrice(!_state.IsCharState(), indexByte2, indexByte);
		_optimum[1].MakeAsChar();
		uint price = _isMatch[(_state.Index << 4) + num4].GetPrice1();
		uint num5 = price + _isRep[_state.Index].GetPrice1();
		if (indexByte2 == indexByte)
		{
			uint num6 = num5 + GetRepLen1Price(_state, num4);
			if (num6 < _optimum[1].Price)
			{
				_optimum[1].Price = num6;
				_optimum[1].MakeAsShortRep();
			}
		}
		uint num7 = ((lenRes >= repLens[num2]) ? lenRes : repLens[num2]);
		if (num7 < 2)
		{
			backRes = _optimum[1].BackPrev;
			return 1u;
		}
		_optimum[1].PosPrev = 0u;
		_optimum[0].Backs0 = reps[0];
		_optimum[0].Backs1 = reps[1];
		_optimum[0].Backs2 = reps[2];
		_optimum[0].Backs3 = reps[3];
		uint num8 = num7;
		do
		{
			_optimum[num8--].Price = 268435455u;
		}
		while (num8 >= 2);
		for (uint num3 = 0u; num3 < 4; num3++)
		{
			uint num9 = repLens[num3];
			if (num9 < 2)
			{
				continue;
			}
			uint num10 = num5 + GetPureRepPrice(num3, _state, num4);
			do
			{
				uint num11 = num10 + _repMatchLenEncoder.GetPrice(num9 - 2, num4);
				Optimal optimal = _optimum[num9];
				if (num11 < optimal.Price)
				{
					optimal.Price = num11;
					optimal.PosPrev = 0u;
					optimal.BackPrev = num3;
					optimal.Prev1IsChar = false;
				}
			}
			while (--num9 >= 2);
		}
		uint num12 = price + _isRep[_state.Index].GetPrice0();
		num8 = ((repLens[0] >= 2) ? (repLens[0] + 1) : 2u);
		if (num8 <= lenRes)
		{
			uint num13;
			for (num13 = 0u; num8 > _matchDistances[num13]; num13 += 2)
			{
			}
			while (true)
			{
				bool flag = true;
				uint num14 = _matchDistances[num13 + 1];
				uint num11 = num12 + GetPosLenPrice(num14, num8, num4);
				Optimal optimal = _optimum[num8];
				if (num11 < optimal.Price)
				{
					optimal.Price = num11;
					optimal.PosPrev = 0u;
					optimal.BackPrev = num14 + 4;
					optimal.Prev1IsChar = false;
				}
				if (num8 == _matchDistances[num13])
				{
					num13 += 2;
					if (num13 == numDistancePairs)
					{
						break;
					}
				}
				num8++;
			}
		}
		uint num15 = 0u;
		uint lenRes2;
		while (true)
		{
			bool flag = true;
			num15++;
			if (num15 == num7)
			{
				return Backward(out backRes, num15);
			}
			ReadMatchDistances(out lenRes2, out numDistancePairs);
			if (lenRes2 >= _numFastBytes)
			{
				break;
			}
			position++;
			uint num16 = _optimum[num15].PosPrev;
			Base.State state;
			if (_optimum[num15].Prev1IsChar)
			{
				num16--;
				if (_optimum[num15].Prev2)
				{
					state = _optimum[_optimum[num15].PosPrev2].State;
					if (_optimum[num15].BackPrev2 < 4)
					{
						state.UpdateRep();
					}
					else
					{
						state.UpdateMatch();
					}
				}
				else
				{
					state = _optimum[num16].State;
				}
				state.UpdateChar();
			}
			else
			{
				state = _optimum[num16].State;
			}
			if (num16 == num15 - 1)
			{
				if (_optimum[num15].IsShortRep())
				{
					state.UpdateShortRep();
				}
				else
				{
					state.UpdateChar();
				}
			}
			else
			{
				uint num17;
				if (_optimum[num15].Prev1IsChar && _optimum[num15].Prev2)
				{
					num16 = _optimum[num15].PosPrev2;
					num17 = _optimum[num15].BackPrev2;
					state.UpdateRep();
				}
				else
				{
					num17 = _optimum[num15].BackPrev;
					if (num17 < 4)
					{
						state.UpdateRep();
					}
					else
					{
						state.UpdateMatch();
					}
				}
				Optimal optimal2 = _optimum[num16];
				switch (num17)
				{
				case 0u:
					reps[0] = optimal2.Backs0;
					reps[1] = optimal2.Backs1;
					reps[2] = optimal2.Backs2;
					reps[3] = optimal2.Backs3;
					break;
				case 1u:
					reps[0] = optimal2.Backs1;
					reps[1] = optimal2.Backs0;
					reps[2] = optimal2.Backs2;
					reps[3] = optimal2.Backs3;
					break;
				case 2u:
					reps[0] = optimal2.Backs2;
					reps[1] = optimal2.Backs0;
					reps[2] = optimal2.Backs1;
					reps[3] = optimal2.Backs3;
					break;
				case 3u:
					reps[0] = optimal2.Backs3;
					reps[1] = optimal2.Backs0;
					reps[2] = optimal2.Backs1;
					reps[3] = optimal2.Backs2;
					break;
				default:
					reps[0] = num17 - 4;
					reps[1] = optimal2.Backs0;
					reps[2] = optimal2.Backs1;
					reps[3] = optimal2.Backs2;
					break;
				}
			}
			_optimum[num15].State = state;
			_optimum[num15].Backs0 = reps[0];
			_optimum[num15].Backs1 = reps[1];
			_optimum[num15].Backs2 = reps[2];
			_optimum[num15].Backs3 = reps[3];
			uint price2 = _optimum[num15].Price;
			indexByte = _matchFinder.GetIndexByte(-1);
			indexByte2 = _matchFinder.GetIndexByte((int)(0 - reps[0] - 1 - 1));
			num4 = position & _posStateMask;
			uint num18 = price2 + _isMatch[(state.Index << 4) + num4].GetPrice0() + _literalEncoder.GetSubCoder(position, _matchFinder.GetIndexByte(-2)).GetPrice(!state.IsCharState(), indexByte2, indexByte);
			Optimal optimal3 = _optimum[num15 + 1];
			bool flag2 = false;
			if (num18 < optimal3.Price)
			{
				optimal3.Price = num18;
				optimal3.PosPrev = num15;
				optimal3.MakeAsChar();
				flag2 = true;
			}
			price = price2 + _isMatch[(state.Index << 4) + num4].GetPrice1();
			num5 = price + _isRep[state.Index].GetPrice1();
			if (indexByte2 == indexByte && (optimal3.PosPrev >= num15 || optimal3.BackPrev != 0))
			{
				uint num6 = num5 + GetRepLen1Price(state, num4);
				if (num6 <= optimal3.Price)
				{
					optimal3.Price = num6;
					optimal3.PosPrev = num15;
					optimal3.MakeAsShortRep();
					flag2 = true;
				}
			}
			uint val = _matchFinder.GetNumAvailableBytes() + 1;
			val = Math.Min(4095 - num15, val);
			num = val;
			if (num < 2)
			{
				continue;
			}
			if (num > _numFastBytes)
			{
				num = _numFastBytes;
			}
			if (!flag2 && indexByte2 != indexByte)
			{
				uint limit = Math.Min(val - 1, _numFastBytes);
				uint matchLen = _matchFinder.GetMatchLen(0, reps[0], limit);
				if (matchLen >= 2)
				{
					Base.State state2 = state;
					state2.UpdateChar();
					uint num19 = (position + 1) & _posStateMask;
					uint num20 = num18 + _isMatch[(state2.Index << 4) + num19].GetPrice1() + _isRep[state2.Index].GetPrice1();
					uint num21 = num15 + 1 + matchLen;
					while (num7 < num21)
					{
						_optimum[++num7].Price = 268435455u;
					}
					uint num11 = num20 + GetRepPrice(0u, matchLen, state2, num19);
					Optimal optimal = _optimum[num21];
					if (num11 < optimal.Price)
					{
						optimal.Price = num11;
						optimal.PosPrev = num15 + 1;
						optimal.BackPrev = 0u;
						optimal.Prev1IsChar = true;
						optimal.Prev2 = false;
					}
				}
			}
			uint num22 = 2u;
			uint num24;
			for (uint num23 = 0u; num23 < 4; num23++)
			{
				num24 = _matchFinder.GetMatchLen(-1, reps[num23], num);
				if (num24 < 2)
				{
					continue;
				}
				uint num25 = num24;
				while (true)
				{
					if (num7 < num15 + num24)
					{
						_optimum[++num7].Price = 268435455u;
						continue;
					}
					uint num11 = num5 + GetRepPrice(num23, num24, state, num4);
					Optimal optimal = _optimum[num15 + num24];
					if (num11 < optimal.Price)
					{
						optimal.Price = num11;
						optimal.PosPrev = num15;
						optimal.BackPrev = num23;
						optimal.Prev1IsChar = false;
					}
					if (--num24 < 2)
					{
						break;
					}
				}
				num24 = num25;
				if (num23 == 0)
				{
					num22 = num24 + 1;
				}
				if (num24 >= val)
				{
					continue;
				}
				uint limit = Math.Min(val - 1 - num24, _numFastBytes);
				uint matchLen = _matchFinder.GetMatchLen((int)num24, reps[num23], limit);
				if (matchLen >= 2)
				{
					Base.State state2 = state;
					state2.UpdateRep();
					uint num19 = (position + num24) & _posStateMask;
					uint num26 = num5 + GetRepPrice(num23, num24, state, num4) + _isMatch[(state2.Index << 4) + num19].GetPrice0() + _literalEncoder.GetSubCoder(position + num24, _matchFinder.GetIndexByte((int)(num24 - 1 - 1))).GetPrice(matchMode: true, _matchFinder.GetIndexByte((int)(num24 - 1 - (reps[num23] + 1))), _matchFinder.GetIndexByte((int)(num24 - 1)));
					state2.UpdateChar();
					num19 = (position + num24 + 1) & _posStateMask;
					uint num27 = num26 + _isMatch[(state2.Index << 4) + num19].GetPrice1();
					uint num20 = num27 + _isRep[state2.Index].GetPrice1();
					uint num21 = num24 + 1 + matchLen;
					while (num7 < num15 + num21)
					{
						_optimum[++num7].Price = 268435455u;
					}
					uint num11 = num20 + GetRepPrice(0u, matchLen, state2, num19);
					Optimal optimal = _optimum[num15 + num21];
					if (num11 < optimal.Price)
					{
						optimal.Price = num11;
						optimal.PosPrev = num15 + num24 + 1;
						optimal.BackPrev = 0u;
						optimal.Prev1IsChar = true;
						optimal.Prev2 = true;
						optimal.PosPrev2 = num15;
						optimal.BackPrev2 = num23;
					}
				}
			}
			if (lenRes2 > num)
			{
				lenRes2 = num;
				for (numDistancePairs = 0u; lenRes2 > _matchDistances[numDistancePairs]; numDistancePairs += 2)
				{
				}
				_matchDistances[numDistancePairs] = lenRes2;
				numDistancePairs += 2;
			}
			if (lenRes2 < num22)
			{
				continue;
			}
			num12 = price + _isRep[state.Index].GetPrice0();
			while (num7 < num15 + lenRes2)
			{
				_optimum[++num7].Price = 268435455u;
			}
			uint num13;
			for (num13 = 0u; num22 > _matchDistances[num13]; num13 += 2)
			{
			}
			num24 = num22;
			while (true)
			{
				flag = true;
				uint num28 = _matchDistances[num13 + 1];
				uint num11 = num12 + GetPosLenPrice(num28, num24, num4);
				Optimal optimal = _optimum[num15 + num24];
				if (num11 < optimal.Price)
				{
					optimal.Price = num11;
					optimal.PosPrev = num15;
					optimal.BackPrev = num28 + 4;
					optimal.Prev1IsChar = false;
				}
				if (num24 == _matchDistances[num13])
				{
					if (num24 < val)
					{
						uint limit = Math.Min(val - 1 - num24, _numFastBytes);
						uint matchLen = _matchFinder.GetMatchLen((int)num24, num28, limit);
						if (matchLen >= 2)
						{
							Base.State state2 = state;
							state2.UpdateMatch();
							uint num19 = (position + num24) & _posStateMask;
							uint num26 = num11 + _isMatch[(state2.Index << 4) + num19].GetPrice0() + _literalEncoder.GetSubCoder(position + num24, _matchFinder.GetIndexByte((int)(num24 - 1 - 1))).GetPrice(matchMode: true, _matchFinder.GetIndexByte((int)(num24 - (num28 + 1) - 1)), _matchFinder.GetIndexByte((int)(num24 - 1)));
							state2.UpdateChar();
							num19 = (position + num24 + 1) & _posStateMask;
							uint num27 = num26 + _isMatch[(state2.Index << 4) + num19].GetPrice1();
							uint num20 = num27 + _isRep[state2.Index].GetPrice1();
							uint num21 = num24 + 1 + matchLen;
							while (num7 < num15 + num21)
							{
								_optimum[++num7].Price = 268435455u;
							}
							num11 = num20 + GetRepPrice(0u, matchLen, state2, num19);
							optimal = _optimum[num15 + num21];
							if (num11 < optimal.Price)
							{
								optimal.Price = num11;
								optimal.PosPrev = num15 + num24 + 1;
								optimal.BackPrev = 0u;
								optimal.Prev1IsChar = true;
								optimal.Prev2 = true;
								optimal.PosPrev2 = num15;
								optimal.BackPrev2 = num28 + 4;
							}
						}
					}
					num13 += 2;
					if (num13 == numDistancePairs)
					{
						break;
					}
				}
				num24++;
			}
		}
		_numDistancePairs = numDistancePairs;
		_longestMatchLength = lenRes2;
		_longestMatchWasFound = true;
		return Backward(out backRes, num15);
	}

	private bool ChangePair(uint smallDist, uint bigDist)
	{
		return smallDist < 33554432 && bigDist >= smallDist << 7;
	}

	private void WriteEndMarker(uint posState)
	{
		if (_writeEndMark)
		{
			_isMatch[(_state.Index << 4) + posState].Encode(_rangeEncoder, 1u);
			_isRep[_state.Index].Encode(_rangeEncoder, 0u);
			_state.UpdateMatch();
			uint num = 2u;
			_lenEncoder.Encode(_rangeEncoder, num - 2, posState);
			uint symbol = 63u;
			uint lenToPosState = Base.GetLenToPosState(num);
			_posSlotEncoder[lenToPosState].Encode(_rangeEncoder, symbol);
			int num2 = 30;
			uint num3 = (uint)((1 << num2) - 1);
			_rangeEncoder.EncodeDirectBits(num3 >> 4, num2 - 4);
			_posAlignEncoder.ReverseEncode(_rangeEncoder, num3 & 0xF);
		}
	}

	private void Flush(uint nowPos)
	{
		ReleaseMFStream();
		WriteEndMarker(nowPos & _posStateMask);
		_rangeEncoder.FlushData();
		_rangeEncoder.FlushStream();
	}

	public void CodeOneBlock(out long inSize, out long outSize, out bool finished)
	{
		inSize = 0L;
		outSize = 0L;
		finished = true;
		if (_inStream != null)
		{
			_matchFinder.SetStream(_inStream);
			_matchFinder.Init();
			_needReleaseMFStream = true;
			_inStream = null;
			if (_trainSize != 0)
			{
				_matchFinder.Skip(_trainSize);
			}
		}
		if (_finished)
		{
			return;
		}
		_finished = true;
		long num = nowPos64;
		if (nowPos64 == 0)
		{
			if (_matchFinder.GetNumAvailableBytes() == 0)
			{
				Flush((uint)nowPos64);
				return;
			}
			ReadMatchDistances(out var _, out var _);
			uint num2 = (uint)(int)nowPos64 & _posStateMask;
			_isMatch[(_state.Index << 4) + num2].Encode(_rangeEncoder, 0u);
			_state.UpdateChar();
			byte indexByte = _matchFinder.GetIndexByte((int)(0 - _additionalOffset));
			_literalEncoder.GetSubCoder((uint)nowPos64, _previousByte).Encode(_rangeEncoder, indexByte);
			_previousByte = indexByte;
			_additionalOffset--;
			nowPos64++;
		}
		if (_matchFinder.GetNumAvailableBytes() == 0)
		{
			Flush((uint)nowPos64);
			return;
		}
		while (true)
		{
			bool flag = true;
			uint lenRes = GetOptimum((uint)nowPos64, out var backRes);
			uint num2 = (uint)(int)nowPos64 & _posStateMask;
			uint num3 = (_state.Index << 4) + num2;
			if (lenRes == 1 && backRes == uint.MaxValue)
			{
				_isMatch[num3].Encode(_rangeEncoder, 0u);
				byte indexByte = _matchFinder.GetIndexByte((int)(0 - _additionalOffset));
				LiteralEncoder.Encoder2 subCoder = _literalEncoder.GetSubCoder((uint)nowPos64, _previousByte);
				if (!_state.IsCharState())
				{
					byte indexByte2 = _matchFinder.GetIndexByte((int)(0 - _repDistances[0] - 1 - _additionalOffset));
					subCoder.EncodeMatched(_rangeEncoder, indexByte2, indexByte);
				}
				else
				{
					subCoder.Encode(_rangeEncoder, indexByte);
				}
				_previousByte = indexByte;
				_state.UpdateChar();
			}
			else
			{
				_isMatch[num3].Encode(_rangeEncoder, 1u);
				if (backRes < 4)
				{
					_isRep[_state.Index].Encode(_rangeEncoder, 1u);
					if (backRes == 0)
					{
						_isRepG0[_state.Index].Encode(_rangeEncoder, 0u);
						if (lenRes == 1)
						{
							_isRep0Long[num3].Encode(_rangeEncoder, 0u);
						}
						else
						{
							_isRep0Long[num3].Encode(_rangeEncoder, 1u);
						}
					}
					else
					{
						_isRepG0[_state.Index].Encode(_rangeEncoder, 1u);
						if (backRes == 1)
						{
							_isRepG1[_state.Index].Encode(_rangeEncoder, 0u);
						}
						else
						{
							_isRepG1[_state.Index].Encode(_rangeEncoder, 1u);
							_isRepG2[_state.Index].Encode(_rangeEncoder, backRes - 2);
						}
					}
					if (lenRes == 1)
					{
						_state.UpdateShortRep();
					}
					else
					{
						_repMatchLenEncoder.Encode(_rangeEncoder, lenRes - 2, num2);
						_state.UpdateRep();
					}
					uint num4 = _repDistances[backRes];
					if (backRes != 0)
					{
						for (uint num5 = backRes; num5 >= 1; num5--)
						{
							_repDistances[num5] = _repDistances[num5 - 1];
						}
						_repDistances[0] = num4;
					}
				}
				else
				{
					_isRep[_state.Index].Encode(_rangeEncoder, 0u);
					_state.UpdateMatch();
					_lenEncoder.Encode(_rangeEncoder, lenRes - 2, num2);
					backRes -= 4;
					uint posSlot = GetPosSlot(backRes);
					uint lenToPosState = Base.GetLenToPosState(lenRes);
					_posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);
					if (posSlot >= 4)
					{
						int num6 = (int)((posSlot >> 1) - 1);
						uint num7 = (2 | (posSlot & 1)) << num6;
						uint num8 = backRes - num7;
						if (posSlot < 14)
						{
							BitTreeEncoder.ReverseEncode(_posEncoders, num7 - posSlot - 1, _rangeEncoder, num6, num8);
						}
						else
						{
							_rangeEncoder.EncodeDirectBits(num8 >> 4, num6 - 4);
							_posAlignEncoder.ReverseEncode(_rangeEncoder, num8 & 0xF);
							_alignPriceCount++;
						}
					}
					uint num4 = backRes;
					for (uint num5 = 3u; num5 >= 1; num5--)
					{
						_repDistances[num5] = _repDistances[num5 - 1];
					}
					_repDistances[0] = num4;
					_matchPriceCount++;
				}
				_previousByte = _matchFinder.GetIndexByte((int)(lenRes - 1 - _additionalOffset));
			}
			_additionalOffset -= lenRes;
			nowPos64 += lenRes;
			if (_additionalOffset == 0)
			{
				if (_matchPriceCount >= 128)
				{
					FillDistancesPrices();
				}
				if (_alignPriceCount >= 16)
				{
					FillAlignPrices();
				}
				inSize = nowPos64;
				outSize = _rangeEncoder.GetProcessedSizeAdd();
				if (_matchFinder.GetNumAvailableBytes() == 0)
				{
					Flush((uint)nowPos64);
					return;
				}
				if (nowPos64 - num >= 4096)
				{
					break;
				}
			}
		}
		_finished = false;
		finished = false;
	}

	private void ReleaseMFStream()
	{
		if (_matchFinder != null && _needReleaseMFStream)
		{
			_matchFinder.ReleaseStream();
			_needReleaseMFStream = false;
		}
	}

	private void SetOutStream(Stream outStream)
	{
		_rangeEncoder.SetStream(outStream);
	}

	private void ReleaseOutStream()
	{
		_rangeEncoder.ReleaseStream();
	}

	private void ReleaseStreams()
	{
		ReleaseMFStream();
		ReleaseOutStream();
	}

	private void SetStreams(Stream inStream, Stream outStream, long inSize, long outSize)
	{
		_inStream = inStream;
		_finished = false;
		Create();
		SetOutStream(outStream);
		Init();
		FillDistancesPrices();
		FillAlignPrices();
		_lenEncoder.SetTableSize(_numFastBytes + 1 - 2);
		_lenEncoder.UpdateTables((uint)(1 << _posStateBits));
		_repMatchLenEncoder.SetTableSize(_numFastBytes + 1 - 2);
		_repMatchLenEncoder.UpdateTables((uint)(1 << _posStateBits));
		nowPos64 = 0L;
	}

	public void Code(Stream inStream, Stream outStream, long inSize, long outSize, ICodeProgress progress)
	{
		_needReleaseMFStream = false;
		try
		{
			SetStreams(inStream, outStream, inSize, outSize);
			while (true)
			{
				bool flag = true;
				CodeOneBlock(out var inSize2, out var outSize2, out var finished);
				if (finished)
				{
					break;
				}
				progress?.SetProgress(inSize2, outSize2);
			}
		}
		finally
		{
			ReleaseStreams();
		}
	}

	public void WriteCoderProperties(Stream outStream)
	{
		properties[0] = (byte)((_posStateBits * 5 + _numLiteralPosStateBits) * 9 + _numLiteralContextBits);
		for (int i = 0; i < 4; i++)
		{
			properties[1 + i] = (byte)((_dictionarySize >> 8 * i) & 0xFF);
		}
		outStream.Write(properties, 0, 5);
	}

	private void FillDistancesPrices()
	{
		for (uint num = 4u; num < 128; num++)
		{
			uint posSlot = GetPosSlot(num);
			int num2 = (int)((posSlot >> 1) - 1);
			uint num3 = (2 | (posSlot & 1)) << num2;
			tempPrices[num] = BitTreeEncoder.ReverseGetPrice(_posEncoders, num3 - posSlot - 1, num2, num - num3);
		}
		for (uint num4 = 0u; num4 < 4; num4++)
		{
			BitTreeEncoder bitTreeEncoder = _posSlotEncoder[num4];
			uint num5 = num4 << 6;
			for (uint posSlot = 0u; posSlot < _distTableSize; posSlot++)
			{
				_posSlotPrices[num5 + posSlot] = bitTreeEncoder.GetPrice(posSlot);
			}
			for (uint posSlot = 14u; posSlot < _distTableSize; posSlot++)
			{
				_posSlotPrices[num5 + posSlot] += (posSlot >> 1) - 1 - 4 << 6;
			}
			uint num6 = num4 * 128;
			uint num;
			for (num = 0u; num < 4; num++)
			{
				_distancesPrices[num6 + num] = _posSlotPrices[num5 + num];
			}
			for (; num < 128; num++)
			{
				_distancesPrices[num6 + num] = _posSlotPrices[num5 + GetPosSlot(num)] + tempPrices[num];
			}
		}
		_matchPriceCount = 0u;
	}

	private void FillAlignPrices()
	{
		for (uint num = 0u; num < 16; num++)
		{
			_alignPrices[num] = _posAlignEncoder.ReverseGetPrice(num);
		}
		_alignPriceCount = 0u;
	}

	private static int FindMatchFinder(string s)
	{
		for (int i = 0; i < kMatchFinderIDs.Length; i++)
		{
			if (s == kMatchFinderIDs[i])
			{
				return i;
			}
		}
		return -1;
	}

	public void SetCoderProperties(CoderPropID[] propIDs, object[] properties)
	{
		for (uint num = 0u; num < properties.Length; num++)
		{
			object obj = properties[num];
			switch (propIDs[num])
			{
			case CoderPropID.NumFastBytes:
				if (!(obj is int num2))
				{
					throw new InvalidParamException();
				}
				if (num2 < 5 || (long)num2 > 273L)
				{
					throw new InvalidParamException();
				}
				_numFastBytes = (uint)num2;
				break;
			case CoderPropID.MatchFinder:
			{
				if (!(obj is string))
				{
					throw new InvalidParamException();
				}
				EMatchFinderType matchFinderType = _matchFinderType;
				int num6 = FindMatchFinder(((string)obj).ToUpper());
				if (num6 < 0)
				{
					throw new InvalidParamException();
				}
				_matchFinderType = (EMatchFinderType)num6;
				if (_matchFinder != null && matchFinderType != _matchFinderType)
				{
					_dictionarySizePrev = uint.MaxValue;
					_matchFinder = null;
				}
				break;
			}
			case CoderPropID.DictionarySize:
			{
				if (!(obj is int num7))
				{
					throw new InvalidParamException();
				}
				if ((long)num7 < 1L || (long)num7 > 1073741824L)
				{
					throw new InvalidParamException();
				}
				_dictionarySize = (uint)num7;
				int i;
				for (i = 0; (long)i < 30L && num7 > (uint)(1 << i); i++)
				{
				}
				_distTableSize = (uint)(i * 2);
				break;
			}
			case CoderPropID.PosStateBits:
				if (!(obj is int num3))
				{
					throw new InvalidParamException();
				}
				if (num3 < 0 || (long)num3 > 4L)
				{
					throw new InvalidParamException();
				}
				_posStateBits = num3;
				_posStateMask = (uint)((1 << _posStateBits) - 1);
				break;
			case CoderPropID.LitPosBits:
				if (!(obj is int num5))
				{
					throw new InvalidParamException();
				}
				if (num5 < 0 || (long)num5 > 4L)
				{
					throw new InvalidParamException();
				}
				_numLiteralPosStateBits = num5;
				break;
			case CoderPropID.LitContextBits:
				if (!(obj is int num4))
				{
					throw new InvalidParamException();
				}
				if (num4 < 0 || (long)num4 > 8L)
				{
					throw new InvalidParamException();
				}
				_numLiteralContextBits = num4;
				break;
			case CoderPropID.EndMarker:
				if (!(obj is bool))
				{
					throw new InvalidParamException();
				}
				SetWriteEndMarkerMode((bool)obj);
				break;
			default:
				throw new InvalidParamException();
			case CoderPropID.Algorithm:
				break;
			}
		}
	}

	public void SetTrainSize(uint trainSize)
	{
		_trainSize = trainSize;
	}
}
