#!/bin/bash

# Script to seed Stripe with 3 sample products and prices

echo "Creating Stripe products and prices..."

# Product 1: Starter Plan
echo "Creating Starter Plan..."
PRODUCT_1=$(stripe products create \
  --id="starter_plan" \
  --name="Starter Plan" \
  --description="Essential features for small teams" \
  | jq -r '.id')

PRICE_1=$(stripe prices create \
  --product="$PRODUCT_1" \
  --unit-amount=999 \
  --currency=usd \
  --recurring.interval="month" \
  | jq -r '.id')

echo "✓ Starter Plan created"

# Product 2: Growth Plan
echo "Creating Growth Plan..."
PRODUCT_2=$(stripe products create \
  --id="growth_plan" \
  --name="Growth Plan" \
  --description="Advanced features for growing teams" \
  | jq -r '.id')

PRICE_2=$(stripe prices create \
  --product="$PRODUCT_2" \
  --unit-amount=2999 \
  --currency=usd \
  --recurring.interval="month" \
  | jq -r '.id')

echo "✓ Growth Plan created"

# Product 3: Professional Plan
echo "Creating Professional Plan..."
PRODUCT_3=$(stripe products create \
  --id="professional_plan" \
  --name="Professional Plan" \
  --description="Full feature set for large organizations" \
  | jq -r '.id')

PRICE_3=$(stripe prices create \
  --product="$PRODUCT_3" \
  --unit-amount=9999 \
  --currency=usd \
  --recurring.interval="month" \
  | jq -r '.id')

echo "✓ Professional Plan created"

echo ""
echo "All products and prices created successfully!"
echo "  Starter Product ID: $PRODUCT_1"
echo "  Starter Price ID: $PRICE_1"
echo "  Growth Product ID: $PRODUCT_2"
echo "  Growth Price ID: $PRICE_2"
echo "  Professional Product ID: $PRODUCT_3"
echo "  Professional Price ID: $PRICE_3"
