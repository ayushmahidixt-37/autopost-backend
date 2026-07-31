-- Example rows for local testing ONLY.
-- These are fictional placeholders, not real company contacts.
-- Do not send real erasure requests to these addresses, and do not
-- treat any pre-populated company row as verified unless you have
-- personally checked it against that company's published privacy
-- policy / grievance officer notice.

insert into companies (name, category, privacy_email, grievance_email, dpo_email, website, notes, verified, source_url)
values
  (
    'Example Bank Pvt Ltd (SAMPLE DATA)',
    'banking',
    'sample-privacy@example.com',
    'sample-grievance@example.com',
    'sample-dpo@example.com',
    'https://example.com',
    'Placeholder row for testing. Replace with a real, verified entry before using this in production.',
    false,
    'https://example.com/privacy-policy'
  ),
  (
    'Example E-commerce Ltd (SAMPLE DATA)',
    'e-commerce',
    'sample-privacy@example.org',
    'sample-grievance@example.org',
    null,
    'https://example.org',
    'Placeholder row for testing. Replace with a real, verified entry before using this in production.',
    false,
    'https://example.org/privacy-policy'
  );
