﻿//Allowed Namespaces
using ChessChallenge.API;
using System; //Debug Only
//using System.Numerics;
using System.Collections.Generic;
//using System.Linq;

public class MyBot : IChessBot
{

    public ByteBoard controlMap = new ByteBoard();
    public ByteBoard whiteControlMap = new ByteBoard();
    public ByteBoard blackControlMap = new ByteBoard();

    float[] materialValues = { 10, 30, 90, 90, 150, 270, 0 };
    float[] pieceControlValues = { 0, 10, 30, 30, 50, 90, 0, 0 };
    float[] emptyControlValues = { 1, 2, 3, 4, 4, 3, 2, 1 };
    float pawnRankMod = 2;


    public Move Think(Board board, Timer timer)
    {
        //DEBUG_DisplayControlMaps(board);
        return MoveSort(board, 50, 0, out float notUsed, timer, timer.MillisecondsRemaining / 40);
    }

    #region Search 


    public Move MoveSort(Board board, int turnsAhead, int maxSearchWidth, out float score, Timer timer, int turnTime)
    {
        Dictionary<Move, float> moveValues = new Dictionary<Move, float>();
        Move[] moves = board.GetLegalMoves();
        score = board.IsWhiteToMove ? int.MinValue : int.MaxValue;

        foreach (Move move in moves)
        {
            if (move.IsPromotion && move.PromotionPieceType != PieceType.Queen) continue;

            board.MakeMove(move);

            float moveScore;
            if (board.IsInCheckmate()) { moveScore = board.IsWhiteToMove ? int.MinValue : int.MaxValue; }//Swapped because move will be undone.
            else if (board.IsDraw()) moveScore = 0;
            else moveScore = EvaluatePosition(board);

            board.UndoMove(move);

            moveValues.Add(move, moveScore);
        }


        List<Move> checkedMoves = new List<Move>();
        for (int i = 0; i < maxSearchWidth; i++)
        {
            if (turnTime - timer.MillisecondsElapsedThisTurn <= 0) checkedMoves.Clear();
            Move highestValueMove = moves[0];
            foreach (Move move in moveValues.Keys)
            {
                if (!checkedMoves.Contains(move) && moveValues[move] > moveValues[highestValueMove]) highestValueMove = move;
            }
            if (turnTime - timer.MillisecondsElapsedThisTurn <= 0) {
                {
                    if (turnsAhead % 2 != 0) return MoveSort(board, turnsAhead - 1, maxSearchWidth, out float notUsed, timer, 0);
                    else return highestValueMove;
                }
            }

            
            
            board.MakeMove(highestValueMove);
            float moveScore;
            if (board.IsInCheckmate()) { moveScore = board.IsWhiteToMove ? int.MinValue : int.MaxValue; }//Swapped because move will be undone.
            else if (board.IsDraw()) moveScore = 0;
            else MoveSort(board, turnsAhead - 1, maxSearchWidth, out moveScore, timer, turnTime);

            moveValues[highestValueMove] = moveScore;
            board.UndoMove(highestValueMove);
            checkedMoves.Add(highestValueMove);
            if (checkedMoves.Count >= moves.Length) break;
        }




        return moves[0];//Code should never reach this
    }


    #endregion

    #region Evaluate

    public float EvaluatePosition(Board board)
    {

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
                if (list.IsWhitePieceList) whiteControlMap.AddBitBoard((BitboardHelper.GetPieceAttacks(piece.PieceType, piece.Square, board, piece.IsWhite)));
                else blackControlMap.AddBitBoard(BitboardHelper.GetPieceAttacks(piece.PieceType, piece.Square, board, piece.IsWhite), -1);
            }
        }

        if (board.IsWhiteToMove) whiteControlMap.AddBitBoard(whiteControlMap.MapOfControlledSquares());
        else blackControlMap.AddBitBoard(blackControlMap.MapOfControlledSquares(), -1);

        controlMap = whiteControlMap + blackControlMap;
    }

    public float GetControlScore(Board board)
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

    public float GetMaterialScore(Board board)
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