using System.Globalization;

namespace GigApp.Api.Models
{
    public static class PartnerKyc
    {
        public const string SkillChangeWarning =
            "Changing your skill sends your account back for KYC approval. "
            + "You will not be able to accept work until an administrator approves the new skill.";

        public static bool ChangeSkill(Partner partner, int categoryId, string? fromName, string? toName)
        {
            if (partner.SkillCategoryId == categoryId) return false;

            partner.SkillCategoryId = categoryId;
            partner.KycReviewNote =
                $"Skill changed from {Describe(fromName)} to {Describe(toName)} on {Today()}.";

            if (partner.KycStatus == KycStatus.Approved)
            {
                partner.KycStatus = KycStatus.Pending;
                partner.KycRejectionReason = null;
                partner.KycReviewedAt = null;
                return true;
            }

            return false;
        }

        public static void SubmitDocuments(Partner partner)
        {
            partner.KycStatus = KycStatus.Pending;
            partner.KycRejectionReason = null;
            partner.KycReviewedAt = null;
            partner.KycReviewNote = $"Documents submitted on {Today()}.";
        }

        public static void Review(Partner partner, bool approved, string? rejectionReason)
        {
            partner.KycStatus = approved ? KycStatus.Approved : KycStatus.Rejected;
            partner.KycRejectionReason = approved ? null : rejectionReason?.Trim();
            partner.KycReviewedAt = DateTime.UtcNow;
            partner.KycReviewNote = null;
        }

        private static string Describe(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "an unknown category" : name;

        private static string Today() =>
            DateTime.UtcNow.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    }
}
