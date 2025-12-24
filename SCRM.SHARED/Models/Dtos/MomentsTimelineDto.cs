using System.Collections.Generic;

namespace SCRM.SHARED.Models.Dtos
{
    public class MomentsTimelineDto
    {
        public long SnsId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        public string Content { get; set; }
        public long CreateTime { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public List<MomentCommentDto> Comments { get; set; } = new List<MomentCommentDto>();
        public List<MomentLikeDto> Likes { get; set; } = new List<MomentLikeDto>();
    }

    public class MomentCommentDto
    {
        public string AuthorName { get; set; }
        public string Content { get; set; }
    }

    public class MomentLikeDto
    {
        public string UserName { get; set; }
        public string NickName { get; set; }
    }
}
