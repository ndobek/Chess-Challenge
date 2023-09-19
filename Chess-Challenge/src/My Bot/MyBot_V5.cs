//Allowed Namespaces
using ChessChallenge.API;
using System; //Debug Only
//using System.Numerics;
using System.Collections.Generic;
//using System.Linq;

public class MyBot_V5 : IChessBot
{
    //Control Maps
    public static ByteBoard controlMap = new ByteBoard();
    public static ByteBoard whiteControlMap = new ByteBoard();
    public static ByteBoard blackControlMap = new ByteBoard();


    //Values given to various board conditions
    static float[] materialValues = { 10, 30, 90, 90, 150, 270, 0 };
    static float[] pieceControlValues = { 0, 10, 30, 30, 50, 90, 0, 0 };
    static float[] emptyControlValues = { 1, 2, 3, 4, 4, 3, 2, 1 };
    static float pawnRankMod = 2;

    Timer _timer;
    int turnTime;


    public Move Think(Board board, Timer timer)
    {
        _timer = timer;
        turnTime = timer.MillisecondsRemaining / 40;

        //DEBUG_DisplayControlMaps(board);
        //Console.WriteLine(timer.MillisecondsRemaining);

        return MoveSort(board, 3, 3);
    }

    #region Search 


    public Move MoveSort(Board board, int turnsAhead, int maxSearchWidth)
    {
        List<MoveInfo> moveInfos = MoveInfo.MoveArrayToMoveInfoList(board.GetLegalMoves());

        for (int turn = 0; turn <= turnsAhead; turn++)
        {
            for(int move =0; move < moveInfos.Count; move++)
            {
                moveInfos[move].Evaluate(board, turn, maxSearchWidth);
            }
        }

        moveInfos.Sort();
        foreach (MoveInfo moveInfo in moveInfos)
        {
            Console.WriteLine(moveInfo.value + moveInfo.move.ToString());
        }

        Console.WriteLine(board.IsWhiteToMove ? moveInfos[0].value : moveInfos[moveInfos.Count - 1].value);
        Console.Write(board.IsWhiteToMove ? moveInfos[0].move.ToString() : moveInfos[moveInfos.Count - 1].move.ToString());
        Console.Write("\n---------------TURN---------------\n");
        return board.IsWhiteToMove ? moveInfos[0].move : moveInfos[moveInfos.Count - 1].move;
    }


    #endregion

    #region Evaluate

    public static void BuildControlMap(Board board)
    {
        PieceList[] pieces = board.GetAllPieceLists();
        controlMap = new ByteBoard();
        whiteControlMap = new ByteBoard();
        blackControlMap = new ByteBoard();
        foreach (PieceList list in pieces)
        {
            foreach (Piece piece in list)
            {
                if (list.IsWhitePieceList) whiteControlMap.AddBitBoard((BitboardHelper.GetPieceAttacks(piece.PieceType, piece.Square, board, piece.IsWhite)));
                else blackControlMap.AddBitBoard(BitboardHelper.GetPieceAttacks(piece.PieceType, piece.Square, board, piece.IsWhite), -1);
            }
        }

        if (board.IsWhiteToMove) whiteControlMap.AddBitBoard(whiteControlMap.MapOfControlledSquares());
        else blackControlMap.AddBitBoard(blackControlMap.MapOfControlledSquares(), -1);

        controlMap = whiteControlMap + blackControlMap;
    }

    public static float GetControlScore(Board board)
    {
        BuildControlMap(board);
        float result = 0;
        ulong kingBitBoard = BitboardHelper.GetKingAttacks(board.GetKingSquare(!board.IsWhiteToMove));
        ByteBoard playerToMoveControlMap = board.IsWhiteToMove ? whiteControlMap : blackControlMap;

        LoopBoard((int i, int j) => {
            Piece piece = board.GetPiece(new Square(i, j));
            ByteBoard enemyControlMap = piece.IsWhite ? blackControlMap : whiteControlMap;
            if (enemyControlMap.GetSquareValue(piece.Square) != 0) result += pieceControlValues[(int)piece.PieceType] * controlMap.SquareSign(piece.Square);

           if(BitboardHelper.SquareIsSet(kingBitBoard,piece.Square) && playerToMoveControlMap.GetSquareValue(piece.Square) != 0)
            {
                result += pieceControlValues[7] * (board.IsWhiteToMove ? 1 : -1);
            }

            result += (emptyControlValues[i] + emptyControlValues[j]) * controlMap.SquareSign(piece.Square);

            if (piece.IsPawn)
            {
                if (piece.IsWhite) result += piece.Square.Rank * pawnRankMod;
                else result -= (9 - piece.Square.Rank) * pawnRankMod;
            }
        });

        result += GetMaterialScore(board);
        return result;
    }

    public static float GetMaterialScore(Board board)
    {
        float result = 0;
        foreach(PieceList piece in board.GetAllPieceLists())
        {
            result += piece.Count * materialValues[(int)piece.TypeOfPieceInList] * (piece.IsWhitePieceList ? 1 : -1);
        }
        return result;
    }


    #endregion

    #region ByteBoard

    public class ByteBoard
    {
        public int[,] byteBoard = new int[8, 8];
        public void AddBitBoard(ulong bitBoard, int value = 1)
        {
            LoopBoard((int i, int j) => {
                byteBoard[i, j] += BitboardHelper.SquareIsSet(bitBoard, new Square(i, j)) ? value : 0;
            });
        }

        public int GetSquareValue(Square square)
        {
            return byteBoard[square.File, square.Rank];
        }
        //public static ByteBoard operator -(ByteBoard left, ByteBoard right)
        //{
        //    ByteBoard result = new ByteBoard();
        //    LoopBoard((int i, int j) => {
        //        result.byteBoard[i, j] =  (left.byteBoard[i, j] - right.byteBoard[i, j]);
        //    });
        //    return result;
        //}
        public static ByteBoard operator +(ByteBoard left, ByteBoard right)
        {
            ByteBoard result = new ByteBoard();
            LoopBoard((int i, int j) => {
                result.byteBoard[i, j] = (left.byteBoard[i, j] + right.byteBoard[i, j]);
            });
            return result;
        }
        //public int SquareSign(int file, int rank)
        //{
        //    return Math.Clamp(byteBoard[file, rank],  -1,  1);
        //}
        public int SquareSign(Square square)
        {
            return Math.Clamp(byteBoard[square.File, square.Rank], -1, 1);
        }

        public ulong MapOfControlledSquares()
        {
            ulong result = 0;
            LoopBoard((int i, int j) => {
                if (byteBoard[i, j] != 0) BitboardHelper.SetSquare(ref result, new Square(i, j));
            });
            return result;
        }

    }

    #endregion

    #region MoveInfo

    public class MoveInfo: IComparable<MoveInfo>
    {
        public Move move;
        public float value;
        public bool moveChecked;
        public List<MoveInfo> nextMoves;

        public static List<MoveInfo> MoveArrayToMoveInfoList(Move[] moves)
        {
            List<MoveInfo> result = new List<MoveInfo>();
            foreach(Move move in moves)
            {
                result.Add(new MoveInfo(move));
            }
            return result;
        }

        public MoveInfo(Move move) 
        { 
            this.move = move; 
        }

        public int CompareTo(MoveInfo? other)
        {
            return value.CompareTo(other.value) * -1;
        }

        public void Evaluate(Board board, int turnsAhead, int maxSearchWidth)
        {
            board.MakeMove(move);

            if (nextMoves == null)
            {
                nextMoves = MoveArrayToMoveInfoList(board.GetLegalMoves());
            }

            if (!moveChecked)
            {
                if (board.IsInCheckmate()) { value = board.IsWhiteToMove ? int.MinValue : int.MaxValue; }//Swapped because move will be undone.
                else if (board.IsDraw()) value = 0;
                else if (move.IsPromotion && move.PromotionPieceType != PieceType.Queen) value = board.IsWhiteToMove ? int.MaxValue : int.MinValue;
                else value = GetControlScore(board);
                moveChecked = true;
            }

            if (turnsAhead > 0)
            {
                for(int i = 0; i < Math.Min(maxSearchWidth, nextMoves.Count); i++)
                {                    
                    nextMoves[i].Evaluate(board, turnsAhead -1, maxSearchWidth);
                    value = board.IsWhiteToMove ? Math.Min(value, nextMoves[i].value): Math.Max(value, nextMoves[i].value);
                }
                nextMoves.Sort();

            }

            board.UndoMove(move);


        }
    }

    #endregion

    #region Helper Functions

    public static void LoopBoard(Action<int, int> action)
    {
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                action(i, j);
            }
        }
    }
    public static Move HighestValueUncheckedMove(ref Dictionary<Move, float> moveValues, ref List<Move> checkedMoves, Board board)
    {
        Move highestValueUncheckedMove = Move.NullMove;
        if (checkedMoves.Count< moveValues.Keys.Count)
        {
            foreach (Move move in moveValues.Keys)
            {
                if (!checkedMoves.Contains(move) &&
                    (board.IsWhiteToMove && moveValues[move] >= moveValues[highestValueUncheckedMove]) ||
                    (!board.IsWhiteToMove && moveValues[move] <= moveValues[highestValueUncheckedMove]))
                {
                    highestValueUncheckedMove = move;
                }
            }
        }
        return highestValueUncheckedMove;
    }

    public bool OutOfTime(float percentageOfTurn = 1)
    {
        return (turnTime * percentageOfTurn) - _timer.MillisecondsElapsedThisTurn < 0;
    }

    #endregion

    #region Debug

    //public void DEBUG_DisplayControlMaps(Board board)
    //{
    //    BuildControlMap(board);
    //    Console.WriteLine();
    //    Console.WriteLine();
    //    Console.WriteLine("Control Map: ");
    //    for (int i = 7; i >= 0; i--)
    //    {
    //        for (int j = 0; j < 8; j++)
    //        {
    //            Console.Write($" {controlMap.byteBoard[j, i],2} ");
    //        }
    //        Console.WriteLine();
    //    }
    //    Console.WriteLine($"Control Score: {GetControlScore(board)}");
    //    Console.WriteLine();

    //}

    #endregion
}