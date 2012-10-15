namespace MonoTorrent.Client
{
    public class BlockEventArgs : TorrentEventArgs
    {
        #region Private Fields

        private Block _block;
        private PeerId _id;
        private Piece _piece;

        #endregion

        #region Public Properties

        /// <summary>
        /// The block whose state changed
        /// </summary>
        public Block Block
        {
            get { return _block; }
        }

        /// <summary>
        /// The piece that the block belongs too
        /// </summary>
        public Piece Piece
        {
            get { return _piece; }
        }

        /// <summary>
        /// The peer who the block has been requested off
        /// </summary>
        public PeerId ID
        {
            get { return _id; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new PeerMessageEventArgs
        /// </summary>       
        internal BlockEventArgs(TorrentManager manager, Block block, Piece piece, PeerId id)
            : base(manager)
        {
            Init(block, piece, id);
        }

        private void Init(Block block, Piece piece, PeerId id)
        {
            _block = block;
            _id = id;
            _piece = piece;
        }

        #endregion

        #region Methods

        public override bool Equals(object obj)
        {
            var args = obj as BlockEventArgs;
            return args != null &&
                   _piece.Equals(args._piece) &&
                   _id.Equals(args._id) &&
                   _block.Equals(args._block);
        }

        public override int GetHashCode()
        {
            return _block.GetHashCode();
        }

        #endregion Methods
    }
}