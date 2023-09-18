﻿//Allowed Namespaces
using ChessChallenge.API;
using System; //Debug Only
//using System.Numerics;
//using System.Collections.Generic;
//using System.Linq;

public class MyBot_V2 : IChessBot
{

    public ByteBoard controlMap = new ByteBoard();
    public ByteBoard whiteControlMap = new ByteBoard();
    public ByteBoard blackControlMap = new ByteBoard();

    int[] pieceControlValues = { 0, 10, 30, 30, 50, 90, 0 };
    int[] emptyControlValues = { 1, 2, 3, 4, 4, 3, 2, 1 };
    int pawnRankMod = 2;


    public Move Think(Board board, Timer timer)
    {
        BuildControlMap(board);
        //DEBUG_DisplayControlMaps(board);
        return FindHighestValueMove(board, 2, out int score);
    }

    #region Search 

    public Move FindHighestValueMove(Board board, int turnsAhead, out int score)
    {
        Move[] moves = board.GetLegalMoves();
        Move result = moves[0];
        score = board.IsWhiteToMove ? int.MinValue : int.MaxValue;

        foreach (Move move in moves)
        {
            board.MakeMove(move);

            int moveScore;
            if (board.IsInCheckmate()) { moveScore = board.IsWhiteToMove ? int.MinValue : int.MaxValue; }//Swapped because move will be undone.
            else if (board.IsDraw()) moveScore = 0;
            else if (turnsAhead > 0) { FindHighestValueMove(board, turnsAhead - 1, out moveScore); }
            else moveScore = EvaluatePosition(board);

            board.UndoMove(move);
            if (move.IsPromotion && move.PromotionPieceType != PieceType.Queen) continue;
            if ((board.IsWhiteToMove && moveScore > score) || (!board.IsWhiteToMove && moveScore < score)) { score = moveScore; result = move; }


            //Console.WriteLine($"MoveScore: {moveScore,4} Score: {score,4} Move: {move,5} Result: {result,5}");
        }
        return result;
    }

    #endregion

    #region Evaluate

    public int EvaluatePosition(Board board)
    {
        BuildControlMap(board);
        return GetControlScore(board);
    }

    public void BuildControlMap(Board board)
    {
        PieceList[] pieces = board.GetAllPieceLists();
        controlMap = new ByteBoard();
        whiteControlMap = new ByteBoard();
        blackControlMap = new ByteBoard();
        foreach (PieceList list in pieces)
        {
            foreach (Piece piece in list)
            {
                if (list.IsWhitePieceList) whiteControlMap.AddBitBoard(GetPieceControlBitBoard(piece, board));
                else blackControlMap.AddBitBoard(GetPieceControlBitBoard(piece, board), -1);
            }
        }

        if (board.IsWhiteToMove) whiteControlMap.AddBitBoard(whiteControlMap.MapOfControlledSquares());
        else blackControlMap.AddBitBoard(blackControlMap.MapOfControlledSquares(), -1);

        controlMap = whiteControlMap + blackControlMap;
    }

    public int GetControlScore(Board board)
    {
        int result = 0;
        LoopBoard((int i, int j) => {
            Piece piece = board.GetPiece(new Square(i, j));
            ByteBoard enemyControlMap = piece.IsWhite ? blackControlMap : whiteControlMap;
            result += pieceControlValues[(int)piece.PieceType] * (piece.IsWhite ? 1 : -1);
            if (enemyControlMap.GetSquareValue(piece.Square) != 0) result += pieceControlValues[(int)piece.PieceType] * controlMap.SquareSign(piece.Square);
            result += (emptyControlValues[i] + emptyControlValues[j]) * controlMap.SquareSign(piece.Square);

            if (piece.IsPawn)
            {
                if (piece.IsWhite) result += piece.Square.Rank * pawnRankMod;
                else result -= (9 - piece.Square.Rank) * pawnRankMod;

            }
        });

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

    #region Helper Functions

    public static void Move(Move move, Board board)
    {
        board.MakeMove(move);
    }
    public static void UndoMove(Move move, Board board)
    {
        board.UndoMove(move);
    }

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

    public static ulong GetPieceControlBitBoard(Piece piece, Board board)
    {
        switch (piece.PieceType)
        {
            case PieceType.Pawn: return BitboardHelper.GetPawnAttacks(piece.Square, piece.IsWhite);
            case PieceType.Knight: return BitboardHelper.GetKnightAttacks(piece.Square);
            case PieceType.Bishop:
            case PieceType.Rook:
            case PieceType.Queen: return BitboardHelper.GetSliderAttacks(piece.PieceType, piece.Square, board);
            case PieceType.King: return BitboardHelper.GetKingAttacks(piece.Square);
            default: return default;
        }
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