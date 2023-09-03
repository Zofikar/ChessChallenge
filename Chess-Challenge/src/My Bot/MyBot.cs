using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class MyBot : IChessBot
{
    int[] PieceVals = { 0, 100, 300, 310, 500, 900, 0 };
    int[] atkVals = { 0, 1, 2, 2, 3, 5, 0 };
    int CheckMate = 100000;
    decimal[] PestoPacked = { 
        9900224743556610877869310144m, 53494929773592646717406330372m, 63361911437403633960110968242m, 55980234549002264723438880465m, 59654145891518603609475170205m, 59654145893093148305727209664m, 69279646213966534308125296162m, 52556599500323481109933979649m, 
        56554440720721343275855494821m, 52891527916600385055821513667m, 64332567214064829318475858112m, 73975673041650402209951751997m, 16425759950871043759799614673m, 76777760120318750607755330853m, 78595648229894077393035655145m, 72397603832076830576582202111m, 
        78889629778405879187202366150m, 66826963659930458069259577335m, 7757653057745239109419795439m, 72705993735964324534956593424m, 74868539370796317355223215318m, 76761759779885689695443545834m, 12431563372940844378304155878m, 78617830389056581075051295267m, 
        4660479870402696979694816762m, 349478860696496719657048846m, 77049583498729585393217502687m, 75201173902974282571807979513m, 2799919366840315712364410882m, 76748703012399528202368911886m, 79220828438297994789472763380m, 74271495670478730626235234052m, 
        19258339923357797006828382752m, 5025717454825805570608087888m, 79214745624079154851546133992m, 69318597643513028336415078665m, 5263606013117200425645043924m, 1557153346508307212252415760m, 1555920715755045318604688651m, 927250862980111682775022852m, 
        79222122551469963312005383427m, 78597065291617332687567123705m, 3748883189489600233040184055m, 16746314055552891992505265211m, 74567630204179871405525430259m, 78304529001881770548856230399m, 633443427640874921396339442m, 64027015645723603412304334600m, 
        12727669886448294598458873591m, 2808500832322018468106737978m, 14568901671372667762396108291m, 1559594753990597590947144223m, 66204276563121428362013370858m, 3116453909225321061862400251m, 74278938118322134367848955677m, 68380357261234488370305304300m, 
        65275887995908017650584125391m, 2487897721964540181596988116m, 73946203683864356149181293809m, 3432320166635870018569768949m, 8385217952651404553569636618m, 75834816852832840822747177242m, 4029302052848854096214228461m, 66201924918364098993134961678m 
    };
    int[] SafetyTable = {
            0,  0,   1,   2,   3,   5,   7,   9,  12,  15,
          18,  22,  26,  30,  35,  39,  44,  50,  56,  62,
          68,  75,  82,  85,  89,  97, 105, 113, 122, 131,
         140, 150, 169, 180, 191, 202, 213, 225, 237, 248,
         260, 272, 283, 295, 307, 319, 330, 342, 354, 366,
         377, 389, 401, 412, 424, 436, 448, 459, 471, 483,
         494, 500
    };
    int[] Pesto;
    static ulong tt_mask = 0x7FFFFF;
    Transposition[] tt = new Transposition[tt_mask+1];
    Board bd;

    public MyBot()
    {
        Pesto = PestoPacked.SelectMany(x => decimal.GetBits(x).Take(3)).SelectMany(BitConverter.GetBytes).Select((x, i) => (i < 128 ? 64 : 0) + (sbyte)x).ToArray();
        Pesto[128] = -167;
        Pesto[149] = 129;
    }

    public Move Think(Board board, Timer timer)
    {
        this.bd = board;
        sbyte currDepth = 1;
        sbyte MaxDepth = (sbyte)Lerp(3,6, 1.4f - BitboardHelper.GetNumberOfSetBits(bd.AllPiecesBitboard) / 20f);
        int time = timer.MillisecondsRemaining / 40;
        while (currDepth < MaxDepth)
        {
            if (timer.MillisecondsElapsedThisTurn > time | NegaMax(currDepth, -CheckMate, CheckMate) > CheckMate - board.PlyCount - 5)
                break;
            currDepth++;
        }
        Transposition t = tt[board.ZobristKey & tt_mask];
        Console.WriteLine($"Best move is {t.move} with value of {t.evaluation} in {currDepth} within {timer.MillisecondsElapsedThisTurn}ms while limit was {time}ms."); // #DEBUG
        return t.move;
    }

    int NegaMax(sbyte depth, int alpha, int beta)
    {
        if (bd.IsInCheckmate()) return -CheckMate + bd.PlyCount;
        int staticEval = StaticEval();
        if (bd.IsRepeatedPosition() || bd.IsInsufficientMaterial() || bd.IsFiftyMoveDraw() ) return (staticEval < -2000 ? 1000 : -30);
        ref Transposition transposition = ref tt[tt_mask & bd.ZobristKey];
        if(transposition.hash==bd.ZobristKey && transposition.depth >= depth)
        {
            if (transposition.flag == 1) return transposition.evaluation;
            if (transposition.flag == 2 && transposition.evaluation >= beta) return transposition.evaluation;
            if (transposition.flag == 3 && transposition.evaluation <= alpha) return transposition.evaluation;
        }
        bool qSearch = depth <= 0 && bd.IsInCheck();
        if (depth <= 0 && !qSearch) return staticEval;
        var moves = bd.GetLegalMoves(qSearch).OrderByDescending(mv => mv.IsCapture || mv.IsPromotion);
        if (!moves.Any()) return (staticEval < -2000 ? 1000 : -30);
        int bestVal = alpha; 
        Move bestMove = moves.First();
        if (qSearch) bestVal = staticEval;
        foreach (Move move in moves)
        {
            bd.MakeMove(move);
            int val = -NegaMax((sbyte)(depth - 1), -beta, -alpha) + (move.IsCastles ? 100 : 0);
            bd.UndoMove(move);
            if (val >= beta)
            {
                bestVal = val;
                break;
            }
            if (val >= bestVal)
            {
                bestVal = val;
                bestMove = move;
            }
        }
        if (!qSearch)
        {
            transposition.evaluation = bestVal;
            transposition.hash = bd.ZobristKey;
            transposition.move = bestMove;
            if (bestVal < alpha)
                transposition.flag = 3;
            else if (bestVal >= beta)
            {
                transposition.flag = 2;
            }
            else transposition.flag = 1;
            transposition.depth = (sbyte)depth;
        }
        return bestVal;
    }

    int StaticEval()
    {
        ulong bitboard = bd.AllPiecesBitboard, whiteKingSafety = GetKingSafetyBoard(true, bd), blackKingSafety = GetKingSafetyBoard(false, bd), whiteAttacksOnKing = 0, blackAttacksOnKing=0;
        BitboardHelper.VisualizeBitboard(whiteKingSafety | blackKingSafety);
        int val = 0, pieceCount = BitboardHelper.GetNumberOfSetBits(bitboard), numberOfWhiteAttacksOnKing=0, numberOfWblackAttacksOnKing=0;
        float stage = 1.5f - pieceCount / 16f;
        while (bitboard != 0)
        {
            int i = BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard);
            Square sq = new Square(i);
            Piece p = bd.GetPiece(sq);
            int attacks = BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks(p.PieceType, sq, bd.AllPiecesBitboard, p.IsWhite))*atkVals[(int)p.PieceType];
            if (p.IsWhite) numberOfWhiteAttacksOnKing += attacks;
            else numberOfWblackAttacksOnKing += attacks;
            int index = (128 * ((int)p.PieceType - 1)) + i;
            val += (Lerp(Pesto[index], Pesto[index + 64], stage) + PieceVals[(int)p.PieceType]) * (p.IsWhite ? 1 : -1);
        }
        val += SafetyTable[Math.Clamp(numberOfWhiteAttacksOnKing, 0, 61)] - SafetyTable[Math.Clamp(numberOfWblackAttacksOnKing, 0, 61)];
        return val * (bd.IsWhiteToMove ? 1 : -1);
    }

    ulong GetKingSafetyBoard(bool isWhite, Board board)
    {
        Square sq = board.GetKingSquare(isWhite);
        ulong bd = BitboardHelper.GetPieceAttacks(PieceType.King, sq, 0, isWhite);
        BitboardHelper.SetSquare(ref bd, sq);
        return bd | (isWhite ? bd << 16 : bd >> 16);
    }

    int Lerp(int val1, int val2, float lerp)
    {
        lerp = Math.Clamp(lerp, 0, 1);
        return (int)Math.Round(val1 * (1f - lerp) + val2 * lerp);
    }


    struct Transposition
    {
        public byte flag;
        public sbyte depth;
        public int evaluation;
        public Move move;
        public ulong hash;
    }
}
