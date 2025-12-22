
DO $$
BEGIN
    -- Delete duplicates, keeping the most recently created one
    DELETE FROM "Contacts" a USING (
      SELECT MIN("Id") as id, "WechatAccountId", "Wxid"
      FROM "Contacts" 
      GROUP BY "WechatAccountId", "Wxid" 
      HAVING COUNT(*) > 1
    ) b
    WHERE a."WechatAccountId" = b."WechatAccountId" 
    AND a."Wxid" = b."Wxid" 
    AND a."Id" <> b.id;
END $$;
